using System.IO.Abstractions;
using NSubstitute;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>Writing a failure down where the DJ is shown it, and never throwing doing it.</summary>
public sealed class LoggerServiceExtensionsTests
{
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
}
