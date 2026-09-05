using AsyncAwaitBestPractices;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Helpers;

/// <summary>Starting work nothing will await, and saying so when it fails.</summary>
/// <remarks>
/// A bare discard leaves the exception on a task nobody observes, so it surfaces at the next
/// garbage collection with nothing left to say what started it, or never surfaces at all. An
/// <c>async void</c> is worse: its exception is rethrown on whichever thread was carrying it, and
/// on the UI thread that closes the application in the middle of an evening. Everything nobody can
/// await goes through here instead.
///
/// This is the route for work that carries no handler of its own. Where a call site already passes
/// one to <c>SafeFireAndForget</c>, that handler stays the only report: the library calls a
/// process-wide default handler in addition to a per-call one rather than instead of it, so the
/// two together would be two log lines and two notifications for one failure.
///
/// Reported with <see cref="ILoggerService.ErrorAsync(string, Exception)"/>, which the application
/// already puts on screen as a notification. The message is therefore what the DJ reads: it says
/// what did not happen, not what threw.
/// </remarks>
/// <param name="logger">
/// Where a failure is reported, or nothing at all. On the way down the container the logger lives
/// in is already gone, and having nowhere to report to is not a reason to let the work throw where
/// nothing can catch it.
/// </param>
/// <param name="notAFailure">
/// Which exceptions are an ordinary part of what the owner is doing rather than something to tell
/// the DJ about, and nothing at all for an owner that has none. Asked the moment an exception
/// arrives, since whether one is ordinary depends on what the owner is doing by then.
/// </param>
public sealed class UnawaitedWork(ILoggerService? logger, Func<Exception, bool>? notAFailure = null)
{
    /// <summary>Runs work nothing can await, and reports what falls out of it.</summary>
    /// <param name="whatFailed">What the person is told did not happen.</param>
    /// <param name="work">The work to start.</param>
    public void Start(string whatFailed, Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Guarded before the task is dropped rather than by a SafeFireAndForget handler on it,
        // because work() can throw before it ever returns a task, and a synchronous throw would
        // then be exactly the crash this exists to prevent.
        Guarded().SafeFireAndForget();

        async Task Guarded()
        {
            try
            {
                await work();
            }
            catch (Exception exception)
            {
                if (notAFailure?.Invoke(exception) != true)
                {
                    logger.Report(whatFailed, exception);
                }
            }
        }
    }
}
