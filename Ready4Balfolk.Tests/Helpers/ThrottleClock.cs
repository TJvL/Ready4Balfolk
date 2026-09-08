using System.Reactive.Concurrency;

namespace Ready4Balfolk.Tests.Helpers;

/// <summary>The clock a panel's throttles run on, moved by the test rather than by the machine.</summary>
/// <remarks>
/// Handed to a view model in place of the default scheduler, so the fraction of a second it waits
/// out before it writes, filters or redraws is time the test spends rather than time it sleeps
/// through. A <see cref="Task.Delay(TimeSpan)"/> longer than the throttle is the same assertion
/// with a margin on it, and the margin is what a loaded build agent eats: the run fails, the retry
/// passes, and everybody learns to press retry.
/// </remarks>
internal sealed class ThrottleClock
{
    /// <summary>Past the slowest throttle any of these panels is fed through, which is 300ms.</summary>
    private static readonly TimeSpan PastTheSlowest = TimeSpan.FromMilliseconds(400);

    private readonly HistoricalScheduler _scheduler = new();

    /// <summary>What the view model is handed in place of the default scheduler.</summary>
    public IScheduler Scheduler => _scheduler;

    /// <summary>Lets every throttle that was waiting run out, and the work behind it run.</summary>
    public void LetTheThrottlesRunOut() => MoveOn(PastTheSlowest);

    /// <summary>Puts this much of the panel's own time behind it, for the timers that are slower.</summary>
    public void MoveOn(TimeSpan howFar) => _scheduler.AdvanceBy(howFar);
}
