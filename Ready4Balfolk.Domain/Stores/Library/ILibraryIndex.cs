namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>The index of what is in the music directory, so a startup can avoid opening files.</summary>
public interface ILibraryIndex : IDisposable
{
    /// <summary>Opens the database and creates the schema if it is not there yet.</summary>
    Task OpenAsync(CancellationToken token = default);

    /// <summary>
    /// Every row, by path. Read once per scan: a scan asks about thousands of files, and answering
    /// each from memory is what keeps an unchanged startup from touching the disk at all.
    /// </summary>
    Task<IReadOnlyDictionary<string, LibraryEntry>> SnapshotByPathAsync(CancellationToken token = default);

    /// <summary>Inserts or updates rows, in one transaction.</summary>
    Task WriteAsync(IReadOnlyCollection<LibraryEntry> entries, CancellationToken token = default);

    /// <summary>Forgets every row whose path is not in the set, after a scan has been through.</summary>
    Task DeleteMissingAsync(IReadOnlyCollection<string> existingPaths, CancellationToken token = default);

    /// <summary>How many files the dance list has nothing to say about.</summary>
    /// <remarks>
    /// A query rather than a number held in memory, so the count survives a restart for free and
    /// the watcher never has to announce anything while the application is running a night.
    /// </remarks>
    Task<int> CountUnresolvedAsync(CancellationToken token = default);
}
