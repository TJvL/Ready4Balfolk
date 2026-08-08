using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Picks a track at random, weighted by the user's dance list.</summary>
/// <remarks>
/// The list is read directly: a category's weight multiplied by a dance's, with a marked category
/// narrowing the pick to what is inside it. There is no second structure to keep in step with it.
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
        var weightedDances = CollectWeightedDances(danceListStore.Current, scope);
        if (weightedDances.Count == 0)
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
        foreach (var (slug, weight) in weightedDances)
        {
            if (!tracksBySlug.TryGetValue(slug, out var matching))
            {
                continue;
            }

            // Split across the tracks, so a dance with forty recordings is no likelier to come up
            // than one with four.
            var weightPerTrack = weight / matching.Count;
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

    private static List<(string Slug, double Weight)> CollectWeightedDances(
        DanceList list, RandomSelectionScope scope)
    {
        switch (scope)
        {
            case RandomSelectionScope.EntireList:
                return CollectFrom(list.Categories, parentWeight: 1.0);

            case RandomSelectionScope.Category category:
            {
                var resolved = ResolveCategory(list.Categories, category.Path);
                // The marked category is the root of the pick, so its own weight no longer
                // says anything: everything under it is being compared with everything else
                // under it.
                return resolved is null ? [] : CollectWithin(resolved, weight: 1.0);
            }

            case RandomSelectionScope.SingleDance single:
            {
                var dance = list.AllDances.FirstOrDefault(
                    d => string.Equals(d.Slug, single.Slug, StringComparison.Ordinal));
                return dance is null || dance.Weight <= 0 ? [] : [(dance.Slug, dance.Weight)];
            }

            default:
                return [];
        }
    }

    private static List<(string Slug, double Weight)> CollectFrom(
        IReadOnlyList<DanceCategory> categories, double parentWeight)
    {
        var result = new List<(string, double)>();
        foreach (var category in categories)
        {
            var categoryWeight = parentWeight * category.Weight;
            if (categoryWeight <= 0)
            {
                // Weight zero means never, and it takes everything under it with it.
                continue;
            }

            result.AddRange(CollectWithin(category, categoryWeight));
        }

        return result;
    }

    private static List<(string Slug, double Weight)> CollectWithin(DanceCategory category, double weight)
    {
        var result = category.Dances
            .Where(dance => dance.Weight > 0)
            .Select(dance => (dance.Slug, weight * dance.Weight))
            .ToList();

        result.AddRange(CollectFrom(category.Categories, weight));
        return result;
    }

    private static DanceCategory? ResolveCategory(IReadOnlyList<DanceCategory> categories, int[] path)
    {
        var level = categories;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= level.Count)
            {
                return null;
            }

            if (i == path.Length - 1)
            {
                return level[path[i]];
            }

            level = level[path[i]].Categories;
        }

        return null;
    }

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
