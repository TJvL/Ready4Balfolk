using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>Where the failure of work nobody awaits ends up, and how often.</summary>
/// <remarks>
/// One class, because both halves of the answer read the same static container: what a view
/// handler resolves its logger from, and what a report the application did not ask for would come
/// out of.
/// </remarks>
public sealed class UnawaitedWorkTests
{
    [Fact]
    public async Task Run_WhenTheWorkFails_ReportsWhatTheHandlerCouldNotDo()
    {
        using var logger = new RecordingLoggerService();
        await using var services = Containing(logger);
        App.UseServices(services);

        Handlers.Run(
            "Failed to preview the track",
            () => throw new InvalidOperationException("Failed to create stream"));

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to preview the track", reported.Message);
        Assert.IsType<InvalidOperationException>(reported.Exception);
    }

    [Fact]
    public async Task Run_WhenTheContainerIsAlreadyGone_StillDoesNotThrow()
    {
        var services = Containing(new NoOpLoggerService());
        await services.DisposeAsync();
        App.UseServices(services);

        var ran = new TaskCompletionSource();

        // A pointer that moves while the window is being torn down. Resolving the logger is the
        // one step of this that can throw, and it happens in a void handler: outside everything
        // the mechanism provides, which would make it the crash the mechanism exists to prevent.
        Assert.Null(Record.Exception(() => Handlers.Run("Failed to move the queue item", () =>
        {
            ran.SetResult();
            return Task.CompletedTask;
        })));

        // And the work itself still runs. Having nowhere left to report to is not a reason to
        // drop what the person asked for.
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OneFailureAtASiteThatReportsItsOwn_ReachesTheLogExactlyOnce()
    {
        using var logger = new RecordingLoggerService();
        await using var services = Containing(logger);

        // The container is what a report the application did not ask for would come out of, which
        // is how this notices one.
        App.UseServices(services);

        // AsyncAwaitBestPractices runs a process-wide default handler in addition to the handler a
        // call site passes. The application has a couple of dozen sites that already say what they
        // could not do, so a default alongside them is two log lines and, since notifications are
        // grouped by message, two toasts for one thing going wrong.
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling(exception =>
            logger.Report("Unhandled fire-and-forget exception", exception));

        try
        {
            ApplicationComposition.UseOneReportPerFailure();

            // What saving a setting onto a read-only file looks like from here.
            Task.FromException(new UnauthorizedAccessException("settings.json is read only"))
                .SafeFireAndForget(exception => logger.Report("Failed to save settings", exception));

            var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Failed to save settings", reported.Message);
            Assert.Single(logger.Errors);
        }
        finally
        {
            SafeFireAndForgetExtensions.RemoveDefaultExceptionHandling();
        }
    }

    private static ServiceProvider Containing(ILoggerService logger) =>
        new ServiceCollection().AddSingleton(logger).BuildServiceProvider();
}
