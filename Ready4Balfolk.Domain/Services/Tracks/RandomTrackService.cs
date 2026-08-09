using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Picks a track at random from the dances a scope reaches.</summary>
/// <remarks>
/// Every dance in the pool is equally likely, and a dance's own tracks share its share. So a dance
/// with forty recordings is no likelier to come up than one with four, which is what stops a
/// well-stocked waltz drowning out everything else in the pool.
/// </remarks>
public sealed class RandomTrackService(
    IDanceListStore danceListStore,
    ITrackStore trackStore,
    IQueueHistoryStore queueHistoryStore,
    IQueueService queueService,
    IQueueConsumptionService consumptionService)
    : IRandomTrackService
{
    private readonly Random _random = new();

    public Models.Tracks.Track? PickRandomTrack(RandomSelectionScope scope, bool allowDuplicates)
    {
        var slugs = CollectDanceSlugs(danceListStore.Current, scope);
        if (slugs.Count == 0)
        {
            return null;
        }

        // Grouped by slug, so a track follows the dance it resolved to rather than whatever the
        // dance happens to be spelled as at the moment.
        var tracksBySlug = trackStore.Current
            .Where(track => track.DanceSlug is not null)
            .GroupBy(track => track.DanceSlug!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var candidates = new List<(Models.Tracks.Track Track, double Weight)>();
        foreach (var slug in slugs)
        {
            if (!tracksBySlug.TryGetValue(slug, out var matching))
            {
                // A dance nobody owns a track for simply cannot come up, which is why the list
                // needs no notion of the dances this user plays.
                continue;
            }

            var weightPerTrack = 1.0 / matching.Count;
            candidates.AddRange(matching.Select(track => (track, weightPerTrack)));
        }

        if (!allowDuplicates)
        {
            var excluded = GetExcludedFilePaths();
            candidates.RemoveAll(candidate => excluded.Contains(candidate.Track.FileInfo.FullName));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var totalWeight = candidates.Sum(candidate => candidate.Weight);
        if (totalWeight <= 0)
        {
            return null;
        }

        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var (track, weight) in candidates)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return track;
            }
        }

        return candidates[^1].Track;
    }

    private static List<string> CollectDanceSlugs(DanceList list, RandomSelectionScope scope) =>
        scope switch
        {
            RandomSelectionScope.Pool pool =>
                [.. list.WithAnyTag(pool.Tags).Select(dance => dance.Slug)],

            // Named outright, so it stands whether or not the dance is in the pool: asking for a
            // hanter dro is an answer, not a filter.
            RandomSelectionScope.SingleDance single =>
                list.FindDance(single.Slug) is null ? [] : [single.Slug],

            _ => []
        };

    private HashSet<string> GetExcludedFilePaths()
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);

        // Exclude tracks that finished in history
        foreach (var entry in queueHistoryStore.Current.Entries)
        {
            if (entry is TrackHistoryEntry { CompletionStatus: CompletionStatus.Finished } track)
            {
                excluded.Add(track.FilePath);
            }
        }

        // Exclude tracks currently in queue
        foreach (var item in queueService.Items)
        {
            var track = item switch
            {
                TrackQueueItem t => t.Track,
                AutoTrackQueueItem a => a.TrackQueueItem.Track,
                _ => null
            };
            if (track is not null)
            {
                excluded.Add(track.FileInfo.FullName);
            }
        }

        // Exclude currently playing track
        var currentItem = consumptionService.CurrentItem;
        var currentTrack = currentItem switch
        {
            TrackQueueItem t => t.Track,
            AutoTrackQueueItem a => a.TrackQueueItem.Track,
            _ => null
        };
        if (currentTrack is not null)
        {
            excluded.Add(currentTrack.FileInfo.FullName);
        }

        return excluded;
    }
}
