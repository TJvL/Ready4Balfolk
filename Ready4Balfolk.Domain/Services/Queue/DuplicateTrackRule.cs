using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class DuplicateTrackRule(
    Func<IQueueItem?> currentItemProvider,
    IQueueHistoryStore historyStore) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => null;

    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
    {
        var filePath = GetFilePath(item);
        if (filePath is null)
        {
            return null;
        }

        if (adjustedItems.Any(i => GetFilePath(i) == filePath))
        {
            return new QueueRuleVerdict(false, DomainStrings.DuplicateTrackRule_AlreadyInQueue);
        }

        var currentPlaying = currentItemProvider();
        if (currentPlaying is not null && GetFilePath(currentPlaying) == filePath)
        {
            return new QueueRuleVerdict(false, DomainStrings.DuplicateTrackRule_CurrentlyPlaying);
        }

        var history = historyStore.Current;
        return history.Entries.OfType<TrackHistoryEntry>()
            .Any(e => e.CompletionStatus == CompletionStatus.Finished && e.FilePath == filePath)
            ? new QueueRuleVerdict(false, DomainStrings.DuplicateTrackRule_AlreadyPlayed)
            : null;
    }

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        var seenPaths = new HashSet<string>();

        // Seed with currently-playing file path
        var currentPlaying = currentItemProvider();
        if (currentPlaying is not null)
        {
            var playingPath = GetFilePath(currentPlaying);
            if (playingPath is not null)
            {
                seenPaths.Add(playingPath);
            }
        }

        // Seed with finished history entries
        var history = historyStore.Current;
        foreach (var entry in history.Entries.OfType<TrackHistoryEntry>())
        {
            if (entry.CompletionStatus == CompletionStatus.Finished)
            {
                seenPaths.Add(entry.FilePath);
            }
        }

        // Iterate queue items — first occurrence kept, duplicates evicted
        var indices = new List<int>();
        for (var i = 0; i < currentItems.Count; i++)
        {
            var filePath = GetFilePath(currentItems[i]);
            if (filePath is not null && !seenPaths.Add(filePath))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    public bool? CanRemove(IQueueItem item) => null;
    public bool? CanMove(IQueueItem item) => null;
    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems) => null;

    private static string? GetFilePath(IQueueItem item) => item switch
    {
        TrackQueueItem t => t.Track.FileInfo.FullName,
        AutoTrackQueueItem a => a.TrackQueueItem.Track.FileInfo.FullName,
        _ => null
    };
}
