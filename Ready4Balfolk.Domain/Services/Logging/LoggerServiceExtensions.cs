using Ready4Balfolk.Domain.Services.Notifications;

namespace Ready4Balfolk.Domain.Services.Logging;

/// <summary>Writing a failure down, and telling the DJ about it when the DJ has to know.</summary>
public static class LoggerServiceExtensions
{
    /// <summary>Writes a failure down, and nowhere else.</summary>
    /// <remarks>
    /// Reported with <see cref="ILoggerService.ErrorAsync(string, Exception)"/>, which only writes
    /// to the log: nothing logged is ever put on screen. <paramref name="logLine" /> is English,
    /// written as a literal at the call site, and is for whoever reads the log, never a resx string.
    ///
    /// Never throws, whatever the reason. It runs on the way out of something that already went
    /// wrong, including on the way down, where the logger can be disposed or gone entirely, and it
    /// writes to a file on a disk that can be full or pulled. Every one of those is a worse thing
    /// to happen mid-evening than a lost line in the log: this is the last thing standing between a
    /// failure and the process, and a reporter that throws while reporting is the crash it exists
    /// to prevent.
    /// </remarks>
    public static void Report(this ILoggerService? logger, string logLine, Exception exception)
    {
        try
        {
            _ = logger?.ErrorAsync(logLine, exception);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Writes a failure down, and tells the DJ what did not happen.</summary>
    /// <remarks>
    /// Two texts for one failure, because the log and the screen are separate. The log line is
    /// English, a literal at the call site, and says what failed for whoever reads the log; the
    /// screen text comes from the resx files, so it is in the DJ's language, and says what did not
    /// happen in the DJ's terms rather than what threw. The exception goes to the log only: its
    /// message is written by whoever threw it, in whatever language, and is no help in front of a
    /// room.
    ///
    /// Never throws, for the reason the log-only report does not, and the same holds for the
    /// screen: on the way down the notifications can be disposed or gone entirely. The DJ is told
    /// first, so a log that cannot be written has no say in whether they are.
    /// </remarks>
    public static void Report(
        this ILoggerService? logger,
        string logLine,
        INotificationService? notifications,
        string screenText,
        Exception exception)
    {
        try
        {
            notifications?.Show(screenText, NotificationSeverity.Error);
        }
        catch (Exception)
        {
        }

        logger.Report(logLine, exception);
    }
}
