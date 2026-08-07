namespace Ready4Balfolk.Web;

/// <summary>Runs a remote command where the rest of the app expects to be called from.</summary>
/// <remarks>
/// SignalR invokes hub methods on threadpool threads, and the queue and the audio engine underneath
/// it are driven from the UI thread. Without this every remote command would be a race that mostly
/// works, which is the worst kind. The host supplies the implementation; the web project has no idea
/// what a UI thread is.
/// </remarks>
public interface IRemoteCommandDispatcher
{
    /// <summary>Runs the work and waits for it.</summary>
    Task InvokeAsync(Func<Task> work);

    /// <summary>Runs the work and waits for its result.</summary>
    Task<T> InvokeAsync<T>(Func<T> work);
}
