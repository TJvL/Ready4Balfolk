using System.Reactive.Linq;
using DynamicData;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class QueueService : IQueueService, IDisposable
{
    private readonly SourceList<IQueueItem> _sourceList = new();
    private readonly IDisposable _settingsSubscription;
    private readonly IDisposable _historySubscription;
    private readonly IDisposable _librarySubscription;
    private readonly IDisposable _libraryMoveSubscription;
    private readonly ILoggerService _loggerService;
    private IQueueGuard _guard;

    public QueueService(
        ISettingsStore settingsStore,
        IQueueHistoryStore historyStore,
        ITrackStore trackStore,
        Func<IQueueItem?> currentItemProvider,
        Func<TimeSpan> currentItemRemainingProvider,
        ILoggerService loggerService,
        TimeProvider time)
    {
        _loggerService = loggerService;
        _guard = QueueGuardBuilder.FromSettings(
            settingsStore.Current, currentItemProvider, historyStore, currentItemRemainingProvider, time);
        _settingsSubscription = settingsStore.Observe()
            .Subscribe(settings =>
            {
                _guard = QueueGuardBuilder.FromSettings(
                    settings, currentItemProvider, historyStore, currentItemRemainingProvider, time);
                Evict();
            });
        _historySubscription = historyStore.Observe()
            .Skip(1)
            .Subscribe(_ => Evict());

        // A queued dance whose file has gone can never play, and leaving it there means the DJ
        // finds out when the room is waiting for it. It goes the moment the file does, the same as
        // it goes from the catalogue.
        _librarySubscription = trackStore.WhenTrackFileVanished.Subscribe(ForgetTracksAt);

        // A file that moved is not one that went: the DJ asked for that dance and it is still
        // there. A queued entry holds the track it was given when it was queued, and nothing in a
        // rebuilt library reaches into the queue, so this is the only thing that re-points it.
        _libraryMoveSubscription = trackStore.WhenTrackFileMoved.Subscribe(RepointTracksAt);
    }

    /// <summary>Takes every queued entry that points at this file out of the queue.</summary>
    private void ForgetTracksAt(string path)
    {
        var removed = RemoveWhere(item => PathOf(item) is { } queued
                                          && string.Equals(queued, path, StringComparison.Ordinal));

        if (removed)
        {
            _ = _loggerService.InfoAsync($"Dropped a queued track whose file has gone: {path}");
        }
    }

    /// <summary>Points every queued entry that was at this path at where the file is now.</summary>
    private void RepointTracksAt(PathMove move)
    {
        var repointed = false;
        _sourceList.Edit(list =>
        {
            for (var index = 0; index < list.Count; index++)
            {
                if (PathOf(list[index]) is not { } queued
                    || !string.Equals(queued, move.From, StringComparison.Ordinal))
                {
                    continue;
                }

                // The row keeps its identity, because a record copy carries the id with it: the
                // DJ's request stays where it is in the queue and the entry on screen does not
                // move under their hand.
                list[index] = Repointed(list[index], move.To);
                repointed = true;
            }
        });

        if (repointed)
        {
            _ = _loggerService.InfoAsync($"A queued track's file moved to: {move.To}");
        }
    }

    private static IQueueItem Repointed(IQueueItem item, string path) => item switch
    {
        TrackQueueItem track => track with { Track = At(track.Track, path) },
        AutoTrackQueueItem auto => auto with
        {
            TrackQueueItem = auto.TrackQueueItem with { Track = At(auto.TrackQueueItem.Track, path) }
        },
        _ => item
    };

    // The same filesystem the track was built against, which in a test is the one holding the
    // fixture and never the real disk.
    private static Track At(Track track, string path) =>
        track with { FileInfo = track.FileInfo.FileSystem.FileInfo.New(path) };

    private static string? PathOf(IQueueItem item) => item switch
    {
        TrackQueueItem track => track.Track.FileInfo.FullName,
        AutoTrackQueueItem auto => auto.TrackQueueItem.Track.FileInfo.FullName,
        _ => null
    };

    public IObservable<IChangeSet<IQueueItem>> Connect() => _sourceList.Connect();

    public int Count => _sourceList.Count;

    public IReadOnlyList<IQueueItem> Items => _sourceList.Items.ToList();

    public IQueueItem? Peek() => _sourceList.Count > 0 ? _sourceList.Items[0] : null;

    public int IndexOf(QueueItemId id) => IndexOf(_sourceList.Items, id);

    private static int IndexOf(IEnumerable<IQueueItem> list, QueueItemId id)
    {
        var index = 0;
        foreach (var item in list)
        {
            if (item.Id == id)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public QueueAddResult Enqueue(IQueueItem item)
    {
        QueueAddResult result = null!;
        _sourceList.Edit(list =>
        {
            result = _guard.EvaluateAdd(item, list.ToList());
            if (!result.Allowed)
            {
                return;
            }

            if (result.RemovalPredicate is { } predicate)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (predicate(list[i]))
                    {
                        list.RemoveAt(i);
                    }
                }
            }

            list.Insert(TailInsertIndex(list, item), item);
        });
        _ = _loggerService.DebugAsync($"Enqueue: {item.GetType().Name}, allowed={result.Allowed}");
        return result;
    }

    // The auto-track sits at the bottom of the queue as a preview of what plays next when nobody
    // requests anything, so real requests go in above it rather than behind it.
    private static int TailInsertIndex(IList<IQueueItem> list, IQueueItem item)
        => ClampInsertIndex(list, item, list.Count);

    private static int ClampInsertIndex(IList<IQueueItem> list, IQueueItem item, int desired)
        => IsPinnedToTail(item)
            ? list.Count
            : Math.Clamp(desired, 0, FirstPinnedIndex(list) ?? list.Count);

    /// <summary>
    /// Entries that belong at the bottom of the queue: the auto-track, which is a preview of what
    /// plays next, and the end of the night, which is the last thing the room hears.
    /// </summary>
    private static bool IsPinnedToTail(IQueueItem item) => item is AutoTrackQueueItem or EndOfNightQueueItem;

    private static int? FirstPinnedIndex(IList<IQueueItem> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (IsPinnedToTail(list[i]))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>Takes the top row off, in one edit.</summary>
    /// <remarks>
    /// Reading and removing were two calls, which is two change sets and a moment in between where
    /// the queue a subscriber sees is not the queue this method thinks it has.
    /// </remarks>
    public IQueueItem? Dequeue()
    {
        IQueueItem? item = null;
        _sourceList.Edit(list =>
        {
            if (list.Count == 0)
            {
                return;
            }

            item = list[0];
            list.RemoveAt(0);
        });

        if (item is not null)
        {
            _ = _loggerService.DebugAsync($"Dequeue: {item.GetType().Name}");
        }

        return item;
    }

    public QueueAddResult InsertAt(int index, IQueueItem item)
    {
        QueueAddResult result = null!;
        _sourceList.Edit(list =>
        {
            result = _guard.EvaluateAdd(item, list.ToList());
            if (!result.Allowed)
            {
                return;
            }

            var removed = 0;
            if (result.RemovalPredicate is { } predicate)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (predicate(list[i]))
                    {
                        list.RemoveAt(i);
                        if (i < index)
                        {
                            removed++;
                        }
                    }
                }
            }

            var target = Math.Max(0, index - removed);
            list.Insert(ClampInsertIndex(list, item, target), item);
        });
        return result;
    }

    // Which row, and where it goes, are answered inside the same edit: the caller's idea of where
    // the row was is a snapshot, and a dance ending anywhere in between would otherwise move the
    // one below it.
    public QueueChangeResult Move(QueueItemId id, int newIndex)
    {
        var result = QueueChangeResult.Gone;
        _sourceList.Edit(list =>
        {
            var oldIndex = IndexOf(list, id);
            if (oldIndex < 0)
            {
                return;
            }

            var item = list[oldIndex];
            if (!_guard.CanMove(item) || newIndex < 0 || newIndex >= list.Count)
            {
                result = QueueChangeResult.Refused;
                return;
            }

            if (!IsPinnedToTail(item) && FirstPinnedIndex(list) is { } pinnedIndex)
            {
                // Removing the item first shifts everything after it down by one.
                newIndex = Math.Min(newIndex, oldIndex < pinnedIndex ? pinnedIndex - 1 : pinnedIndex);
            }

            list.Move(oldIndex, newIndex);
            result = QueueChangeResult.Done;
        });

        return result;
    }

    public QueueChangeResult Remove(QueueItemId id)
    {
        var result = QueueChangeResult.Gone;
        _sourceList.Edit(list =>
        {
            var index = IndexOf(list, id);
            if (index < 0)
            {
                return;
            }

            if (!_guard.CanRemove(list[index]))
            {
                result = QueueChangeResult.Refused;
                return;
            }

            list.RemoveAt(index);
            result = QueueChangeResult.Done;
        });

        return result;
    }

    public bool Clear()
    {
        if (!_guard.CanClear(_sourceList.Items.ToList()))
        {
            return false;
        }

        // Clearing removes the requests, not the auto-track: it is a placeholder for an empty
        // queue, so it survives and simply becomes the only entry again.
        _sourceList.Edit(list =>
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is not AutoTrackQueueItem)
                {
                    list.RemoveAt(i);
                }
            }
        });
        _ = _loggerService.DebugAsync("Queue cleared");
        return true;
    }

    public bool RemoveWhere(Func<IQueueItem, bool> predicate)
    {
        var removed = false;
        _sourceList.Edit(list =>
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (predicate(list[i]))
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }
        });
        return removed;
    }

    private void Evict()
    {
        var items = _sourceList.Items.ToList();
        if (items.Count == 0)
        {
            return;
        }

        var indices = _guard.GetEvictionIndices(items);
        if (indices.Count == 0)
        {
            return;
        }

        // The guard answers in positions against the snapshot above, and the queue can move on
        // while it is answering: a dance ending shifts every row up, and the positions it named
        // then point at rows it never looked at. Name the rows here, and look each one up again
        // inside the edit, because taking one out shifts the rest below it as well.
        var doomed = new List<QueueItemId>(indices.Count);
        foreach (var index in indices)
        {
            if (index >= 0 && index < items.Count)
            {
                doomed.Add(items[index].Id);
            }
        }

        _sourceList.Edit(list =>
        {
            foreach (var id in doomed)
            {
                var index = IndexOf(list, id);
                if (index >= 0)
                {
                    list.RemoveAt(index);
                }
            }
        });
    }

    public void Dispose()
    {
        _settingsSubscription.Dispose();
        _historySubscription.Dispose();
        _librarySubscription.Dispose();
        _libraryMoveSubscription.Dispose();
        _sourceList.Dispose();
    }
}
