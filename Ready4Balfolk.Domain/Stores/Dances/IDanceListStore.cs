using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Stores.Dances;

/// <summary>
/// Holds the published dance list. Read-only on purpose: the list is shared vocabulary, so the
/// application takes it as it is published and has no opinion to store about it.
/// </summary>
public interface IDanceListStore : ILoadableStore, IDisposable
{
    /// <summary>The list as it stands.</summary>
    DanceList Current { get; }

    /// <summary>A lookup over <see cref="Current"/>, rebuilt with it and never separately.</summary>
    DanceListIndex Index { get; }

    DanceListStatus Status { get; }

    IObservable<DanceList> Observe();

    IObservable<DanceListStatus> ObserveStatus();

    /// <summary>The cached copy if there is a usable one, otherwise no list at all: none is shipped.</summary>
    Task LoadAsync(CancellationToken token);

    /// <summary>Downloads the published list and takes it whole if it is newer.</summary>
    Task<DanceListUpdate> RefreshAsync(CancellationToken token = default);

    /// <summary>Takes a list from a file, for a machine that never reaches the internet.</summary>
    Task<DanceListUpdate> UpdateFromFileAsync(IFileInfo sourceFileInfo, CancellationToken token = default);
}
