using Ready4Balfolk.Domain.Models.History;

namespace Ready4Balfolk.Domain.Stores.History;

public interface IQueueHistoryStore : ILoadableStore, IDisposable
{
    /// <summary>The night that is running, which is an empty one until something happens in it.</summary>
    QueueHistory Current { get; }

    IObservable<QueueHistory> Observe();
    Task LoadAsync(CancellationToken token);
    Task AddAsync(QueueHistoryEntry entry);

    /// <summary>Files the current night and opens the next one.</summary>
    /// <remarks>
    /// Nothing is thrown away, which is why this can happen on its own when the end of the night has
    /// played: the evening is kept, and what the user sees is that it is no longer tonight.
    /// </remarks>
    Task EndNightAsync();

    /// <summary>Throws the current night away.</summary>
    /// <remarks>Explicit and confirmed, and no longer the thing anybody reaches for at the end of an evening.</remarks>
    Task DeleteNightAsync();

    Task ExportAsync(string destinationPath);
}
