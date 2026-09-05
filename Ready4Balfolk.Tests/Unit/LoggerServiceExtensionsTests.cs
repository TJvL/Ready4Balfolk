using System.IO.Abstractions;
using NSubstitute;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The one thing standing between a file BASS will not open and a closed application.
/// </summary>
/// <remarks>
/// Every handler that cannot await its own work goes through this, so what it does with an
/// exception is the whole of the fix: report it where the person is shown it, and let nothing
/// escape to the process-level handler, which on the UI thread ends the evening.
/// </remarks>
public sealed class LoggerServiceExtensionsTests
{
    [Fact]
    public async Task RunUnawaited_WorkThrowsAfterAnAwait_ReportsItAndNothingEscapes()
    {
        using var logger = new RecordingLoggerService();
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;

        // Set while the work is started, because an async void sends what it could not return to
        // whichever context was current when it began. That is the route this has to close.
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            logger.RunUnawaited("Failed to preview the track", async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("Failed to create stream");
            });
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to preview the track", reported.Message);
        Assert.IsType<InvalidOperationException>(reported.Exception);
        Assert.Empty(context.Escaped);
    }

    [Fact]
    public async Task RunUnawaited_WorkThrowsBeforeItReturnsATask_ReportsIt()
    {
        using var logger = new RecordingLoggerService();

        // Nothing on the returned task could catch this one: there is no returned task. It is the
        // reason the work is wrapped rather than handed a SafeFireAndForget handler.
        logger.RunUnawaited(
            "Failed to export the log",
            () => throw new IOException("the folder cannot be written"));

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Failed to export the log", reported.Message);
        Assert.IsType<IOException>(reported.Exception);
    }

    [Fact]
    public async Task RunUnawaited_WorkSucceeds_ReportsNothing()
    {
        using var logger = new RecordingLoggerService();
        var ran = new TaskCompletionSource();

        logger.RunUnawaited("Failed to preview the track", () =>
        {
            ran.SetResult();
            return Task.CompletedTask;
        });

        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void Report_WhenTheReportItselfCannotBeMade_DoesNotThrow()
    {
        var disposed = Substitute.For<ILoggerService>();
        disposed.ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>())
            .Returns(_ => throw new ObjectDisposedException(nameof(ILoggerService)));

        var unwritable = Substitute.For<ILoggerService>();
        unwritable.ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>())
            .Returns(_ => throw new IOException("the log is on a stick that was pulled"));

        // On the way down everything a report needs may already be disposed, or gone entirely, and
        // the log is a file on a disk that can fill up. A reporter that throws while reporting
        // replaces a line in the log with the crash it was there to avoid, so none of these may.
        Assert.Null(Record.Exception(() =>
            disposed.Report("Failed to stop the preview", new InvalidOperationException())));
        Assert.Null(Record.Exception(() =>
            unwritable.Report("Failed to stop the preview", new InvalidOperationException())));
        Assert.Null(Record.Exception(() =>
            ((ILoggerService?)null).Report("Failed to stop the preview", new InvalidOperationException())));
    }

    [Fact]
    public async Task Report_ReachesTheStreamTheNotificationsAreDrawnFrom()
    {
        var directory = new FileSystem().DirectoryInfo.New(
            Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        using var logger = new FileLoggerService(directory);

        var seen = new TaskCompletionSource<LogEntry>();
        using var subscription = logger.WhenErrorLogged.Subscribe(entry => seen.TrySetResult(entry));

        try
        {
            // What makes this the fix rather than a line nobody reads: the same stream the startup
            // wiring turns into a notice on screen.
            logger.Report("Failed to preview the track", new InvalidOperationException("boom"));

            var entry = await seen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.Equal("Failed to preview the track", entry.Message);
            Assert.IsType<InvalidOperationException>(entry.Exception);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Catches what an async void could not return, which is where the crash came from.</summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly List<Exception> _escaped = [];

        public IReadOnlyList<Exception> Escaped
        {
            get
            {
                lock (_escaped)
                {
                    return [.. _escaped];
                }
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);

            try
            {
                d(state);
            }
            catch (Exception exception)
            {
                lock (_escaped)
                {
                    _escaped.Add(exception);
                }
            }
        }
    }
}
