using System;
using ReactiveUI;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.UI.Services;

/// <summary>What a command does with a failure of its own.</summary>
/// <remarks>
/// A command whose failure nobody observes ends at ReactiveUI's default handler, and what the DJ
/// read then was "Unhandled RxApp exception": true, and no help in front of a room. Reported here
/// instead, with what did not happen, the way a handler's failure is (<see cref="Handlers" />).
/// </remarks>
internal static class CommandFailures
{
    /// <summary>Says what did not happen whenever the command fails.</summary>
    /// <returns>The subscription, for the view model to dispose with the rest of its own.</returns>
    public static IDisposable ReportFailures(
        this IHandleObservableErrors command, ILoggerService logger, string whatFailed) =>
        command.ThrownExceptions.Subscribe(exception => logger.Report(whatFailed, exception));
}
