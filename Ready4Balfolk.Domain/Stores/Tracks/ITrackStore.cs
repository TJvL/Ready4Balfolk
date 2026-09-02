using DynamicData;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public interface ITrackStore : ILoadableStore
{
    IReadOnlyList<Track> Current { get; }

    /// <summary>
    /// How many indexed tracks the gate is holding out of the library, replayed to new subscribers.
    /// </summary>
    /// <remarks>
    /// Derived from the same rebuild that publishes the library, so it counts everything the review
    /// screen shows: missing fields, a dance the list does not carry, and a file changed since its
    /// approval alike. A SQL count once knew only the first kind and the badge lied.
    /// </remarks>
    IObservable<int> InReviewCount { get; }

    /// <summary>The path of a track whose file has just gone from the music directory.</summary>
    /// <remarks>
    /// Published as the watcher notices it, rather than being read off the library: a rebuild
    /// clears the list and fills it again, so what leaves the library is not the same question as
    /// what left the disk, and only the second one means an entry in the queue can never play.
    /// </remarks>
    IObservable<string> WhenTrackFileVanished { get; }

    /// <summary>Brings the library into line with what the settings now say.</summary>
    /// <remarks>
    /// One call rather than three setters, so the music directory, the declared rules and the dance
    /// rule cannot be applied in an order that makes the store scan the library twice. What changed
    /// decides how much work happens.
    /// </remarks>
    Task ApplyAsync(TrackLibraryConfiguration configuration);

    /// <summary>
    /// Rebuilds the library from the index, through the review gate. Opens no audio files.
    /// </summary>
    /// <remarks>
    /// What review approves has to show up in the library at once, and everything needed to do that
    /// is already indexed. This is how an approval becomes a track without a rescan.
    /// </remarks>
    Task RefreshLibraryAsync(CancellationToken cancellationToken = default);

    IObservable<IChangeSet<Track>> Connect();
    IObservable<IChangeSet<Track>> Connect(IObservable<string> searchText);
}
