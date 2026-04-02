using Ready4Balfolk.Domain.Models.History;

namespace Ready4Balfolk.Domain.Stores.History;

public interface IQueueHistoryStore : ILoadableStore, IDisposable
{
    QueueHistory Current { get; }
    IObservable<QueueHistory> Observe();
    Task LoadAsync(CancellationToken token);
    Task AddAsync(QueueHistoryEntry entry);
    Task ClearAsync();
    Task ExportAsync(FileInfo destinationFileInfo);
}
