using DynamicData;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public interface ITrackStore : ILoadableStore
{
    IReadOnlyList<Track> Current { get; }
    DirectoryInfo? MusicDirectory { set; }

    /// <summary>
    /// What the user has declared about their library's shape. Setting something new re-reads the
    /// library, because a declaration is meant to answer files that are already sitting there.
    /// </summary>
    DiscoverySettings DiscoverySettings { set; }
    IObservable<IChangeSet<Track>> Connect();
    IObservable<IChangeSet<Track>> Connect(IObservable<string> searchText);
}
