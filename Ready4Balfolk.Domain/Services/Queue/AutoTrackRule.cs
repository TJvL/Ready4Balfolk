using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class AutoTrackRule(bool autoQueueEnabled) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => newItem is not AutoTrackQueueItem ? item => item is AutoTrackQueueItem : null;

    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
        => item is AutoTrackQueueItem && adjustedItems.Count > 0
            ? new QueueRuleVerdict(false, "Auto-track can only be added to an empty queue.")
            : null;

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        if (autoQueueEnabled)
            return [];

        return currentItems
            .Select((item, index) => (item, index))
            .Where(x => x.item is AutoTrackQueueItem)
            .Select(x => x.index)
            .ToList();
    }

    public bool? CanRemove(IQueueItem item)
        => item is AutoTrackQueueItem ? false : null;

    public bool? CanMove(IQueueItem item)
        => item is AutoTrackQueueItem ? false : null;

    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems)
        => currentItems.All(i => i is AutoTrackQueueItem) ? false : null;
}
