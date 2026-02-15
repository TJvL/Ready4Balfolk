using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Domain.Stores.Tree;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class RandomTrackService(
    IDanceTreeStore danceTreeStore,
    ITrackStore trackStore,
    IQueueHistoryStore queueHistoryStore,
    IQueueService queueService,
    IQueueConsumptionService consumptionService)
    : IRandomTrackService
{
    private readonly Random _random = new();

    public Models.Tracks.Track? PickRandomTrack(RandomSelectionScope scope, bool allowDuplicates)
    {
        var roots = danceTreeStore.Current;
        var weightedLeaves = CollectWeightedLeaves(roots, scope);

        if (weightedLeaves.Count == 0)
            return null;

        var tracks = trackStore.Current;
        var tracksByDance = tracks
            .GroupBy(t => StringNormalizer.Normalize(t.Dance))
            .ToDictionary(g => g.Key, g => g.ToList());

        var candidates = new List<(Models.Tracks.Track Track, double Weight)>();
        foreach (var leaf in weightedLeaves)
        {
            var normalizedName = StringNormalizer.Normalize(leaf.Name);
            if (!tracksByDance.TryGetValue(normalizedName, out var matchingTracks))
                continue;

            var weightPerTrack = leaf.EffectiveWeight / matchingTracks.Count;
            candidates.AddRange(matchingTracks.Select(track => (track, weightPerTrack)));
        }

        if (candidates.Count == 0)
            return null;

        if (!allowDuplicates)
        {
            var excluded = GetExcludedFilePaths();
            candidates.RemoveAll(c => excluded.Contains(c.Track.FileInfo.FullName));
        }

        if (candidates.Count == 0)
            return null;

        var totalWeight = candidates.Sum(c => c.Weight);
        if (totalWeight <= 0)
            return null;

        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var (track, weight) in candidates)
        {
            cumulative += weight;
            if (roll < cumulative)
                return track;
        }

        return candidates[^1].Track;
    }

    private static List<WeightedLeaf> CollectWeightedLeaves(
        IReadOnlyList<DanceBranch> roots, RandomSelectionScope scope)
    {
        return scope switch
        {
            RandomSelectionScope.EntireTree => CollectFromBranches(roots, 1.0),
            RandomSelectionScope.Subtree subtree => CollectFromSubtree(roots, subtree.BranchPath),
            RandomSelectionScope.SingleDance single => CollectSingleLeaf(roots, single.ParentPath, single.LeafIndex),
            _ => []
        };
    }

    private static List<WeightedLeaf> CollectFromBranches(
        IEnumerable<DanceBranch> branches, double parentWeight)
    {
        var result = new List<WeightedLeaf>();
        foreach (var branch in branches)
        {
            var branchWeight = parentWeight * branch.Weight;
            if (branchWeight <= 0)
                continue;

            foreach (var leaf in branch.Leafs)
            {
                var leafWeight = branchWeight * leaf.Weight;
                if (leafWeight > 0)
                    result.Add(new WeightedLeaf(leaf.Name, leafWeight));
            }

            result.AddRange(CollectFromBranches(branch.Branches.ToList(), branchWeight));
        }

        return result;
    }

    private static List<WeightedLeaf> CollectFromSubtree(
        IReadOnlyList<DanceBranch> roots, IReadOnlyList<int> branchPath)
    {
        var branch = ResolveBranch(roots, branchPath);
        if (branch is null)
            return [];

        var result = (from leaf in branch.Leafs where leaf.Weight > 0 select new WeightedLeaf(leaf.Name, leaf.Weight)).ToList();

        result.AddRange(CollectFromBranches(branch.Branches.ToList(), 1.0));
        return result;
    }

    private static List<WeightedLeaf> CollectSingleLeaf(
        IReadOnlyList<DanceBranch> roots, IReadOnlyList<int> parentPath, int leafIndex)
    {
        var branch = ResolveBranch(roots, parentPath);
        if (branch is null)
            return [];

        var leafs = branch.Leafs.ToList();
        if (leafIndex < 0 || leafIndex >= leafs.Count)
            return [];

        var leaf = leafs[leafIndex];
        return leaf.Weight > 0
            ? [new WeightedLeaf(leaf.Name, leaf.Weight)]
            : [];
    }

    private static DanceBranch? ResolveBranch(IReadOnlyList<DanceBranch> roots, IReadOnlyList<int> path)
    {
        var level = roots;
        for (var i = 0; i < path.Count; i++)
        {
            if (path[i] < 0 || path[i] >= level.Count)
                return null;
            if (i == path.Count - 1)
                return level[path[i]];
            level = level[path[i]].Branches.ToList();
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
                excluded.Add(track.FilePath);
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
                excluded.Add(track.FileInfo.FullName);
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
            excluded.Add(currentTrack.FileInfo.FullName);

        return excluded;
    }

    private sealed record WeightedLeaf(string Name, double EffectiveWeight);
}
