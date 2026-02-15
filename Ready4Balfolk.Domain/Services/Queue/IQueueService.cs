using DynamicData;
using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueService
{
    IObservable<IChangeSet<IQueueItem>> Connect();

    int Count { get; }
    IReadOnlyList<IQueueItem> Items { get; }
    IQueueItem? Peek();

    QueueAddResult Enqueue(IQueueItem item);
    IQueueItem? Dequeue();
    QueueAddResult InsertAt(int index, IQueueItem item);
    bool Move(int oldIndex, int newIndex);
    bool RemoveAt(int index);
    bool Clear();
    bool RemoveWhere(Func<IQueueItem, bool> predicate);
}
