using DynamicData;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public interface ITrackStore : ILoadableStore
{
    IReadOnlyList<Track> Current { get; }
    DirectoryInfo? MusicDirectory { set; }
    IObservable<IChangeSet<Track>> Connect();
    IObservable<IChangeSet<Track>> Connect(IObservable<string> searchText);
}
