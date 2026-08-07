using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.UI.Services;

/// <summary>Runs remote commands on the UI thread.</summary>
/// <remarks>
/// SignalR hands hub methods a threadpool thread. The queue, the settings store and the BASS
/// handles underneath them all expect the UI thread, so without this every button on the phone
/// would be a race that works right up until it does not.
/// </remarks>
public sealed class AvaloniaRemoteCommandDispatcher : IRemoteCommandDispatcher
{
    public Task InvokeAsync(Func<Task> work) => Dispatcher.UIThread.InvokeAsync(work);

    // The generic overload hands back a DispatcherOperation rather than a Task, unlike the
    // Func<Task> one above.
    public Task<T> InvokeAsync<T>(Func<T> work) => Dispatcher.UIThread.InvokeAsync(work).GetTask();
}
