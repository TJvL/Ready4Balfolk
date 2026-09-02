using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class QueueConsumptionService : IQueueConsumptionService, IDisposable
{
    private readonly IAudioPlaybackService _audio;
    private readonly IQueueService _queue;
    private readonly IQueueHistoryStore _history;
    private readonly ILoggerService _loggerService;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CompositeDisposable _globalDisposables = [];
    private CompositeDisposable? _itemDisposables;

    private readonly BehaviorSubject<IQueueItem?> _currentItem = new(null);
    private readonly BehaviorSubject<TimeSpan> _elapsed = new(TimeSpan.Zero);
    private readonly BehaviorSubject<TimeSpan> _totalDuration = new(TimeSpan.Zero);
    private readonly BehaviorSubject<bool> _isPlaying = new(false);
    private readonly Subject<Unit> _itemCompleted = new();

    private bool _itemFinishedNaturally;
    // Captured when the item starts, since the history entry is only built once it ends.
    private DateTime? _currentItemStartedAt;

    public IQueueItem? CurrentItem => _currentItem.Value;
    public IObservable<IQueueItem?> WhenCurrentItemChanged => _currentItem.AsObservable();
    public TimeSpan CurrentItemRemaining =>
        _totalDuration.Value > _elapsed.Value ? _totalDuration.Value - _elapsed.Value : TimeSpan.Zero;

    public IObservable<TimeSpan> WhenElapsedChanged => _elapsed.AsObservable();
    public IObservable<TimeSpan> WhenTotalDurationChanged => _totalDuration.AsObservable();
    public IObservable<bool> WhenIsPlayingChanged => _isPlaying.AsObservable();
    public IObservable<Unit> WhenItemCompleted => _itemCompleted.AsObservable();

    public QueueConsumptionService(
        IAudioPlaybackService audio,
        IQueueService queue,
        IQueueHistoryStore history,
        ILoggerService loggerService)
    {
        _audio = audio;
        _queue = queue;
        _history = history;
        _loggerService = loggerService;

        // The consumption service manages all advancement, so auto-advance is disabled on audio
        _audio.AutoAdvance = false;

        // Global audio play state subscriptions
        _globalDisposables.Add(
            _audio.WhenPlaybackStarted.Subscribe(_ => _isPlaying.OnNext(true)));
        _globalDisposables.Add(
            _audio.WhenPlaybackPaused.Subscribe(_ => _isPlaying.OnNext(false)));
        _globalDisposables.Add(
            _audio.WhenPlaybackRestarted.Subscribe(_ => _isPlaying.OnNext(true)));
        _globalDisposables.Add(
            _audio.WhenPlaybackCleared.Subscribe(_ => _isPlaying.OnNext(false)));
    }

    public async Task AdvanceAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await RecordCurrentItemAsync(
                _itemFinishedNaturally ? CompletionStatus.Finished : CompletionStatus.Skipped);
            CleanupCurrentItem();

            var item = _queue.Dequeue();
            if (item != null)
            {
                _ = _loggerService.DebugAsync($"Advancing to: {item.GetType().Name}");
                await StartItemAsync(item);
            }
            else
            {
                _currentItem.OnNext(null);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PlayPauseAsync()
    {
        if (_audio.IsPlaying)
        {
            await _audio.PauseAsync();
        }
        else
        {
            await _audio.PlayAsync();
        }
    }

    public async Task RestartAsync() => await _audio.RestartAsync();

    public async Task SeekAsync(TimeSpan position)
    {
        await _audio.SeekAsync(position);
        _elapsed.OnNext(position);
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await RecordCurrentItemAsync(CompletionStatus.Skipped);
            CleanupCurrentItem();
            await _audio.ClearAsync();
            _currentItem.OnNext(null);
            _ = _loggerService.DebugAsync("Playback cleared");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Starts an item, and moves past anything that turns out not to be playable.</summary>
    /// <remarks>
    /// A file can go between being queued and being reached: moved in another window, on a drive
    /// that was unplugged, or renamed while the application was closed. The room is waiting either
    /// way, so the evening steps over it and says which track it was, rather than stopping on an
    /// item that can never start.
    /// </remarks>
    private async Task StartItemAsync(IQueueItem item)
    {
        var next = item;

        while (next is not null)
        {
            if (await TryStartItemAsync(next))
            {
                PreloadNext();
                return;
            }

            await RecordCurrentItemAsync(CompletionStatus.Skipped);
            CleanupCurrentItem();
            next = _queue.Dequeue();
        }

        _currentItem.OnNext(null);
    }

    /// <summary>Starts one item. False when it is audio that would not open.</summary>
    private async Task<bool> TryStartItemAsync(IQueueItem item)
    {
        _itemFinishedNaturally = false;
        _currentItemStartedAt = DateTime.Now;
        _itemDisposables = [];
        _elapsed.OnNext(TimeSpan.Zero);
        _totalDuration.OnNext(TimeSpan.Zero);

        switch (item)
        {
            case AutoTrackQueueItem auto:
                _currentItem.OnNext(item);
                return await TryStartAudioAsync(auto.TrackQueueItem.Track.FileInfo.FullName, item.Description);
            case TrackQueueItem track:
                _currentItem.OnNext(item);
                return await TryStartAudioAsync(track.Track.FileInfo.FullName, item.Description);
            case DelayQueueItem delay:
                await _audio.ClearAsync();
                _currentItem.OnNext(item);
                StartCountdown(delay.DelayDuration);
                return true;
            case MessageQueueItem message:
                await _audio.ClearAsync();
                _currentItem.OnNext(item);
                if (message.Duration is { } duration)
                {
                    StartCountdown(duration);
                }
                else
                {
                    _totalDuration.OnNext(TimeSpan.Zero);
                }

                return true;
            case StopQueueItem:
                await _audio.ClearAsync();
                _currentItem.OnNext(item);
                return true;
            case EndOfNightQueueItem endOfNight:
                _currentItem.OnNext(item);
                return await TryStartAudioAsync(endOfNight.FilePath, item.Description);
            default:
                return true;
        }
    }

    /// <summary>Opens a file and starts it, or says which one would not open.</summary>
    /// <remarks>
    /// The failure is reported rather than thrown. Thrown, it reached the application's unhandled
    /// handler, and what a hall's DJ saw was the words "Unhandled RxApp exception" while the music
    /// stopped.
    /// </remarks>
    private async Task<bool> TryStartAudioAsync(string filePath, string description)
    {
        var uri = new Uri(filePath);

        // Subscribe BEFORE starting playback so we don't miss events
        _itemDisposables!.Add(
            _audio.WhenProgressChanged.Subscribe(_elapsed.OnNext));
        _itemDisposables.Add(
            _audio.WhenDurationChanged.Take(1).Subscribe(_totalDuration.OnNext));
        _itemDisposables.Add(
            _audio.WhenPlaybackEnded.Take(1).Subscribe(_ => OnTrackEnded()));

        try
        {
            await _audio.SelectAsync(uri);
            await _audio.PlayAsync();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            _ = _loggerService.ErrorAsync(
                string.Format(CultureInfo.CurrentCulture, DomainStrings.Queue_CannotPlay, description),
                exception);

            return false;
        }
    }

    private void OnTrackEnded()
    {
        _itemFinishedNaturally = true;
        _isPlaying.OnNext(false);
        // Fire-and-forget advance: the gate ensures serialization
        _ = AdvanceAsync();
    }

    private void StartCountdown(TimeSpan duration)
    {
        _totalDuration.OnNext(duration);
        _elapsed.OnNext(TimeSpan.Zero);

        var start = DateTimeOffset.UtcNow;
        _itemDisposables!.Add(
            Observable.Interval(TimeSpan.FromMilliseconds(100))
                .Subscribe(tick =>
                {
                    var elapsed = DateTimeOffset.UtcNow - start;
                    if (elapsed >= duration)
                    {
                        _elapsed.OnNext(duration);
                        _itemFinishedNaturally = true;
                        _ = AdvanceAsync();
                    }
                    else
                    {
                        _elapsed.OnNext(elapsed);
                    }
                }));
    }

    private void PreloadNext()
    {
        var next = _queue.Peek();
        var uri = next switch
        {
            TrackQueueItem t => new Uri(t.Track.FileInfo.FullName),
            AutoTrackQueueItem a => new Uri(a.TrackQueueItem.Track.FileInfo.FullName),
            EndOfNightQueueItem e => new Uri(e.FilePath),
            _ => null
        };

        _ = uri != null ? _audio.PreloadNextAsync(uri) : _audio.ClearPreloadAsync();
    }

    private async Task RecordCurrentItemAsync(CompletionStatus status)
    {
        var item = _currentItem.Value;
        if (item == null)
        {
            return;
        }

        QueueHistoryEntry entry = item switch
        {
            AutoTrackQueueItem auto => new TrackHistoryEntry(
                auto.TrackQueueItem.Track.FileInfo.FullName,
                auto.TrackQueueItem.Track.Dance,
                auto.TrackQueueItem.Track.Artist,
                auto.TrackQueueItem.Track.Title,
                auto.TrackQueueItem.Track.Length,
                auto.TrackQueueItem.RandomlyAdded,
                status,
                _currentItemStartedAt),
            TrackQueueItem track => new TrackHistoryEntry(
                track.Track.FileInfo.FullName,
                track.Track.Dance,
                track.Track.Artist,
                track.Track.Title,
                track.Track.Length,
                track.RandomlyAdded,
                status,
                _currentItemStartedAt),
            MessageQueueItem message => new MessageHistoryEntry(
                message.Description,
                message.Duration,
                status,
                _currentItemStartedAt),
            DelayQueueItem delay => new DelayHistoryEntry(
                delay.DelayDuration,
                status,
                _currentItemStartedAt),
            StopQueueItem => new StopHistoryEntry(status, _currentItemStartedAt),
            EndOfNightQueueItem endOfNight => new EndOfNightHistoryEntry(
                endOfNight.Duration,
                status,
                _currentItemStartedAt),
            _ => throw new InvalidOperationException($"Unknown queue item type: {item.GetType()}")
        };

        await _history.AddAsync(entry);

        // The evening being over is the moment to file it. Nothing can follow the end of the night
        // in the queue, so this is the last thing that will ever happen in this night, and the next
        // one starts clean without anybody having to remember to press anything while packing up.
        if (entry is EndOfNightHistoryEntry)
        {
            await _history.EndNightAsync();
        }

        _itemCompleted.OnNext(Unit.Default);
    }

    private void CleanupCurrentItem()
    {
        _itemDisposables?.Dispose();
        _itemDisposables = null;
        _itemFinishedNaturally = false;
        _currentItemStartedAt = null;
    }

    public void Dispose()
    {
        _itemDisposables?.Dispose();
        _globalDisposables.Dispose();
        _currentItem.Dispose();
        _elapsed.Dispose();
        _totalDuration.Dispose();
        _isPlaying.Dispose();
        _itemCompleted.Dispose();
        _gate.Dispose();
    }
}
