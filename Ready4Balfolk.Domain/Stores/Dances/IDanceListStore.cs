using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Stores.Dances;

public interface IDanceListStore : ILoadableStore, IDisposable
{
    /// <summary>The list as it stands.</summary>
    DanceList Current { get; }

    /// <summary>A lookup over <see cref="Current"/>, rebuilt with it and never separately.</summary>
    DanceListIndex Index { get; }

    IObservable<DanceList> Observe();

    Task LoadAsync(CancellationToken token);

    Task UpdateAsync(Func<DanceList, DanceList> transform);

    /// <summary>Replaces the list wholesale, as the setup wizard does when it builds one.</summary>
    Task ReplaceAsync(DanceList list);

    Task ExportAsync(FileInfo destinationFileInfo);

    Task ImportAsync(FileInfo sourceFileInfo);
}
