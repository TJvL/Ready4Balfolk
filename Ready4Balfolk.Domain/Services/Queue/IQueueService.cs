using DynamicData;
using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueService
{
    IObservable<IChangeSet<IQueueItem>> Connect();

    int Count { get; }
    IReadOnlyList<IQueueItem> Items { get; }
    IQueueItem? Peek();

    /// <summary>Where that row is now, or -1 when it is no longer queued.</summary>
    int IndexOf(QueueItemId id);

    QueueAddResult Enqueue(IQueueItem item);
    IQueueItem? Dequeue();
    QueueAddResult InsertAt(int index, IQueueItem item);

    /// <summary>Moves that row to a position, whatever position it is in when this arrives.</summary>
    QueueChangeResult Move(QueueItemId id, int newIndex);

    /// <summary>Takes that row out, and only that row.</summary>
    QueueChangeResult Remove(QueueItemId id);

    bool Clear();
    bool RemoveWhere(Func<IQueueItem, bool> predicate);
}
