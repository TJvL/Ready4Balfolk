using System.Globalization;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>
/// Refuses entries once the queue would run past a chosen time of day, so a ball can be wound down
/// on schedule. Mirrors the projection shown under the queue: current item's remaining time plus the
/// queued durations.
/// </summary>
/// <remarks>
/// A stop request has no knowable length by design (a delay or a timed message is the way to say how
/// long a pause lasts), so once one is queued there is no end time to judge and the cutoff stops
/// refusing anything rather than pretending to know.
/// </remarks>
public sealed class QueueCutoffRule(
    TimeSpan cutoff,
    TimeSpan grace,
    Func<TimeSpan> currentItemRemainingProvider,
    Func<DateTime> nowProvider) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => null;

    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
    {
        // Control items are how the user manages the evening, and the auto-track is a placeholder
        // rather than a request, so none of them are refused.
        if (item is AutoTrackQueueItem || IsHalt(item))
        {
            return null;
        }

        if (adjustedItems.Any(IsHalt))
        {
            return null;
        }

        var projectedEnd = nowProvider()
                           + currentItemRemainingProvider()
                           + adjustedItems.Aggregate(TimeSpan.Zero, (sum, queued) => sum + (queued.Duration ?? TimeSpan.Zero))
                           + (item.Duration ?? TimeSpan.Zero);

        var now = nowProvider();
        var limit = now.Date + cutoff;
        if (limit - now > TimeSpan.FromHours(12))
        {
            // A ball that has run past midnight: today's instance of the cutoff is most of a day
            // away, so the one that matters belongs to the evening in progress, yesterday.
            limit = limit.AddDays(-1);
        }

        limit += grace;

        return projectedEnd > limit
            ? new QueueRuleVerdict(false, string.Format(CultureInfo.CurrentCulture,
                DomainStrings.QueueCutoffRule_PastCutoff, (now.Date + cutoff).ToString("HH:mm", CultureInfo.CurrentCulture)))
            : null;
    }

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems) => [];

    public bool? CanRemove(IQueueItem item) => null;

    public bool? CanMove(IQueueItem item) => null;

    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems) => null;

    /// <summary>An item that pauses the queue for an unknowable length of time.</summary>
    public static bool IsHalt(IQueueItem item) =>
        item is StopQueueItem or MessageQueueItem { Duration: null };
}
