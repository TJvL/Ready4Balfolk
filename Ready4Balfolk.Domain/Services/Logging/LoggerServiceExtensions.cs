namespace Ready4Balfolk.Domain.Services.Logging;

/// <summary>Writing a failure down where the person is told about it too.</summary>
public static class LoggerServiceExtensions
{
    /// <summary>Writes a failure down, where the person is told about it too.</summary>
    /// <remarks>
    /// Reported with <see cref="ILoggerService.ErrorAsync(string, Exception)"/>, which the
    /// application already puts on screen as a notification. The message is therefore what the DJ
    /// reads: it says what did not happen, not what threw.
    ///
    /// Never throws, whatever the reason. It runs on the way out of something that already went
    /// wrong, including on the way down, where the logger can be disposed or gone entirely, and it
    /// writes to a file on a disk that can be full or pulled. Every one of those is a worse thing
    /// to happen mid-evening than a lost line in the log: this is the last thing standing between a
    /// failure and the process, and a reporter that throws while reporting is the crash it exists
    /// to prevent.
    /// </remarks>
    public static void Report(this ILoggerService? logger, string whatFailed, Exception exception)
    {
        try
        {
            _ = logger?.ErrorAsync(whatFailed, exception);
        }
        catch (Exception)
        {
        }
    }
}
