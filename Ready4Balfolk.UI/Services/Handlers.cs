using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;

namespace Ready4Balfolk.UI.Services;

/// <summary>What a view does with the work a handler starts and cannot await.</summary>
/// <remarks>
/// An event handler is <c>async void</c> by its signature, so an exception out of one is thrown at
/// the process-level handler and the application closes. A file BASS will not open is ordinary
/// here rather than exceptional, and the review queue is precisely where those end up, so clicking
/// preview on one used to end the evening. Handlers hand their work to this instead, and the
/// failure becomes an English line in the log and a notice on screen from the resx files.
///
/// A static reaching into the container, because a view is built by the XAML loader and has no
/// constructor to be given anything through: the same reason the code-behind around it resolves
/// what it needs from <see cref="App.Services"/>.
/// </remarks>
internal static class Handlers
{
    /// <summary>Runs what a handler cannot await, and says so when it fails.</summary>
    /// <param name="logLine">What failed, in English, for the log.</param>
    /// <param name="screenText">What the DJ is told did not happen, from the resx files.</param>
    /// <param name="work">The work to start.</param>
    public static void Run(string logLine, string screenText, Func<Task> work) =>
        new UnawaitedWork(Resolve<ILoggerService>(), Resolve<INotificationService>())
            .Start(logLine, screenText, work);

    /// <summary>A service, or nothing when the container is already gone.</summary>
    /// <remarks>
    /// Asking the container is the one step here that can throw, and it is reached from a void
    /// handler, outside everything below it: a pointer that moves while the window is being torn
    /// down arrives after the provider was disposed, and an unguarded resolve would then be the
    /// crash this class exists to prevent. Nothing left to report to is not a reason to take the
    /// application down; the work still runs, and its failure is dropped rather than thrown.
    /// </remarks>
    private static T? Resolve<T>() where T : class
    {
        try
        {
            return App.Services?.GetService<T>();
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }
}
