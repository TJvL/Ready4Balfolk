using System.Globalization;
using NSubstitute;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>Writing a failure down, telling the DJ about it in two separate texts, and never throwing doing it.</summary>
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

        var screenGone = Substitute.For<INotificationService>();
        screenGone.When(screen => screen.Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>()))
            .Do(_ => throw new ObjectDisposedException(nameof(INotificationService)));

        // On the way down everything a report needs may already be disposed, or gone entirely, and
        // the log is a file on a disk that can fill up. A reporter that throws while reporting
        // replaces a line in the log with the crash it was there to avoid, so none of these may.
        Assert.Null(Record.Exception(() =>
            disposed.Report("Failed to stop the preview", new InvalidOperationException())));
        Assert.Null(Record.Exception(() =>
            unwritable.Report("Failed to stop the preview", new InvalidOperationException())));
        Assert.Null(Record.Exception(() =>
            ((ILoggerService?)null).Report("Failed to stop the preview", new InvalidOperationException())));
        Assert.Null(Record.Exception(() => unwritable.Report(
            "Failed to stop the preview", screenGone, "Het beluisteren kon niet gestopt worden",
            new InvalidOperationException())));
        Assert.Null(Record.Exception(() => ((ILoggerService?)null).Report(
            "Failed to stop the preview", null, "Het beluisteren kon niet gestopt worden",
            new InvalidOperationException())));
    }

    /// <summary>The log gets the English line, the DJ gets the resx text, and neither gets the other's.</summary>
    /// <remarks>
    /// Said in Dutch, where the two are visibly not the same sentence. That is the whole point of
    /// the decision: the log is English and read by whoever fixes things, the screen follows the
    /// language and is read by the DJ, and nothing couples the two.
    /// </remarks>
    [Fact]
    public void Report_LogsTheEnglishLine_AndShowsTheResxText()
    {
        using var logger = new RecordingLoggerService();
        var notifications = Substitute.For<INotificationService>();
        var language = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("nl");
        try
        {
            var failure = new UnauthorizedAccessException("Access to the path 'settings.json.tmp' is denied.");

            logger.Report("Failed to save settings", notifications, DomainStrings.Settings_SaveFailed, failure);

            var logged = Assert.Single(logger.Errors);
            Assert.Equal("Failed to save settings", logged.Message);
            Assert.Same(failure, logged.Exception);
            notifications.Received(1).Show("De instellingen konden niet opgeslagen worden", NotificationSeverity.Error);
            notifications.Received(1).Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
        }
        finally
        {
            CultureInfo.CurrentUICulture = language;
        }
    }

    [Fact]
    public void Report_WithNoScreenText_OnlyWritesTheLog()
    {
        using var logger = new RecordingLoggerService();

        logger.Report("Could not read the length of 'the end of the night.mp3'", new InvalidDataException());

        Assert.Equal("Could not read the length of 'the end of the night.mp3'", Assert.Single(logger.Errors).Message);
    }
}
