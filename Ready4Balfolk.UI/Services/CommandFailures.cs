using System;
using ReactiveUI;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;

namespace Ready4Balfolk.UI.Services;

/// <summary>What a command does with a failure of its own.</summary>
/// <remarks>
/// A command whose failure nobody observes ends at ReactiveUI's default handler, and what the DJ
/// read then was "Unhandled RxApp exception": true, and no help in front of a room. Reported here
/// instead, with what did not happen, the way a handler's failure is (<see cref="Handlers" />):
/// an English line to the log, and a text from the resx files to the screen.
/// </remarks>
internal static class CommandFailures
{
    /// <summary>Says what did not happen whenever the command fails.</summary>
    /// <param name="command">The command whose failures are reported.</param>
    /// <param name="logger">Where the failure is written down.</param>
    /// <param name="logLine">What failed, in English, for the log.</param>
    /// <param name="notifications">Where the DJ is told.</param>
    /// <param name="screenText">What the DJ is told did not happen, from the resx files.</param>
    /// <returns>The subscription, for the view model to dispose with the rest of its own.</returns>
    public static IDisposable ReportFailures(
        this IHandleObservableErrors command,
        ILoggerService logger,
        string logLine,
        INotificationService notifications,
        string screenText) =>
        command.ThrownExceptions.Subscribe(exception =>
            logger.Report(logLine, notifications, screenText, exception));
}
