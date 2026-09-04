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
    /// <para>
    /// Nothing is thrown away, which is why this can happen on its own when the end of the night has
    /// played: the evening is kept, and what the user sees is that it is no longer tonight.
    /// </para>
    /// <para>
    /// <paramref name="endedAt"/> is when the evening actually stopped, for a night that is being
    /// filed after the fact. A night nobody ended is noticed at the next start, which can be days
    /// later, and stamping it with the moment somebody answered the question would put an evening
    /// in the books that ran until Tuesday. Null means now, which is right for a night being ended
    /// as it happens.
    /// </para>
    /// </remarks>
    Task EndNightAsync(DateTime? endedAt = null);

    /// <summary>Every night on file, newest first, including the one that is running.</summary>
    /// <remarks>
    /// A night that is filed is still an evening somebody may want to read, hand over or throw
    /// away. Without this the history file grew for the life of the application with nothing
    /// anybody could do about it, and the account of an evening vanished from the screen the moment
    /// it ended.
    /// </remarks>
    Task<IReadOnlyList<NightSummary>> ListNightsAsync();

    /// <summary>One night, read whole. Null when there is no night with that id.</summary>
    Task<QueueHistory?> ReadNightAsync(long nightId);

    /// <summary>Throws one night away, whether it is running or filed.</summary>
    /// <remarks>Explicit and confirmed, and no longer the thing anybody reaches for at the end of an evening.</remarks>
    Task DeleteNightAsync(long nightId);

    /// <summary>Writes one night out as JSON, whether it is running or filed.</summary>
    Task ExportAsync(long nightId, string destinationPath);
}
