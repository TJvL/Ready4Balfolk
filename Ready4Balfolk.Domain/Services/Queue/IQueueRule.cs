using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueRule
{
    Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems);
    QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems);
    IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems);
    bool? CanRemove(IQueueItem item);
    bool? CanMove(IQueueItem item);
    bool? CanClear(IReadOnlyList<IQueueItem> currentItems);
}
