using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueGuard
{
    QueueAddResult EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> currentItems);
    IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems);
    bool CanRemove(IQueueItem item);
    bool CanMove(IQueueItem item);
    bool CanClear(IReadOnlyList<IQueueItem> currentItems);
}
