using System.Reactive.Linq;
using DynamicData;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class QueueService : IQueueService, IDisposable
{
    private readonly SourceList<IQueueItem> _sourceList = new();
    private readonly IDisposable _settingsSubscription;
    private readonly IDisposable _historySubscription;
    private readonly ILoggerService _loggerService;
    private IQueueGuard _guard;

    public QueueService(
        ISettingsStore settingsStore,
        IQueueHistoryStore historyStore,
        Func<IQueueItem?> currentItemProvider,
        ILoggerService loggerService)
    {
        _loggerService = loggerService;
        _guard = QueueGuardBuilder.FromSettings(settingsStore.Current, currentItemProvider, historyStore);
        _settingsSubscription = settingsStore.Observe()
            .Subscribe(settings =>
            {
                _guard = QueueGuardBuilder.FromSettings(settings, currentItemProvider, historyStore);
                Evict();
            });
        _historySubscription = historyStore.Observe()
            .Skip(1)
            .Subscribe(_ => Evict());
    }

    public IObservable<IChangeSet<IQueueItem>> Connect() => _sourceList.Connect();

    public int Count => _sourceList.Count;

    public IReadOnlyList<IQueueItem> Items => _sourceList.Items.ToList();

    public IQueueItem? Peek() => _sourceList.Count > 0 ? _sourceList.Items[0] : null;

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
        => item is AutoTrackQueueItem
            ? list.Count
            : Math.Clamp(desired, 0, FirstAutoTrackIndex(list) ?? list.Count);

    private static int? FirstAutoTrackIndex(IList<IQueueItem> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is AutoTrackQueueItem)
            {
                return i;
            }
        }

        return null;
    }

    public IQueueItem? Dequeue()
    {
        if (_sourceList.Count == 0)
        {
            return null;
        }

        var item = _sourceList.Items[0];
        _sourceList.RemoveAt(0);
        _ = _loggerService.DebugAsync($"Dequeue: {item.GetType().Name}");
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

    public bool Move(int oldIndex, int newIndex)
    {
        var item = _sourceList.Items[oldIndex];
        if (!_guard.CanMove(item))
        {
            return false;
        }

        if (item is not AutoTrackQueueItem && FirstAutoTrackIndex([.. _sourceList.Items]) is { } autoIndex)
        {
            // Removing the item first shifts everything after it down by one.
            newIndex = Math.Min(newIndex, oldIndex < autoIndex ? autoIndex - 1 : autoIndex);
        }

        _sourceList.Move(oldIndex, newIndex);
        return true;
    }

    public bool RemoveAt(int index)
    {
        if (!_guard.CanRemove(_sourceList.Items[index]))
        {
            return false;
        }

        _sourceList.RemoveAt(index);
        return true;
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

        _sourceList.Edit(list =>
        {
            foreach (var i in indices)
            {
                list.RemoveAt(i);
            }
        });
    }

    public void Dispose()
    {
        _settingsSubscription.Dispose();
        _historySubscription.Dispose();
        _sourceList.Dispose();
    }
}
