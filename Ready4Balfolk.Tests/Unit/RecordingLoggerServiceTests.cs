using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The waiting the tests around unawaited work are built on.</summary>
public sealed class RecordingLoggerServiceTests
{
    [Fact]
    public async Task NextErrorAsync_ReturnsTheReportItWaitedFor_RatherThanWhicheverIsNewest()
    {
        using var logger = new RecordingLoggerService();

        await logger.ErrorAsync("Failed to prepare the next item", new InvalidOperationException());
        await logger.ErrorAsync("Failed to move on to the next item in the queue", new IOException());

        // Handing back the newest entry instead would let a test wait for one report and then
        // assert about a different one, which is a test that says nothing while passing.
        var first = await logger.NextErrorAsync(TestContext.Current.CancellationToken);
        var second = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to prepare the next item", first.Message);
        Assert.Equal("Failed to move on to the next item in the queue", second.Message);
    }

    [Fact]
    public async Task NextErrorAsync_WhenNothingIsReported_GivesUpRatherThanHanging()
    {
        using var logger = new RecordingLoggerService();

        await Assert.ThrowsAsync<TimeoutException>(
            () => logger.NextErrorAsync(TestContext.Current.CancellationToken));

        // The wait that gave up is cancelled rather than left pending, so it cannot take the
        // release belonging to the next thing somebody waits for.
        await logger.ErrorAsync("Failed to export the log", new IOException());
        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to export the log", reported.Message);
    }
}
