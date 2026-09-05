using AsyncAwaitBestPractices;

namespace Ready4Balfolk.Domain.Services.Logging;

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
public static class LoggerServiceExtensions
{
    /// <summary>Runs work nothing can await, and reports what falls out of it.</summary>
    /// <param name="logger">
    /// Where the failure is reported, or nothing at all. On the way down the container the logger
    /// lives in is already gone, and having nowhere to report to is not a reason to let the work
    /// throw where nothing can catch it.
    /// </param>
    /// <param name="whatFailed">What the person is told did not happen.</param>
    /// <param name="work">The work to start.</param>
    public static void RunUnawaited(this ILoggerService? logger, string whatFailed, Func<Task> work)
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
                logger.Report(whatFailed, exception);
            }
        }
    }

    /// <summary>Writes a failure down, where the person is told about it too.</summary>
    /// <remarks>
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
