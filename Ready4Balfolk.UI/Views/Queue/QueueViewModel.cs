using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Queue;

public sealed partial class QueueViewModel : ReactiveObject, IDisposable
{
    private readonly TrackEditorService _trackEditor;
    private readonly IQueueService _queueService;
    private readonly IEndOfNightAudio _endOfNightAudio;
    private readonly IQueueConsumptionService _consumptionService;
    private readonly ISettingsStore _settingsStore;
    private readonly IRandomTrackService _randomTrackService;
    private readonly IDancePool _dancePool;
    private readonly IConfirmationService _confirmationService;
    private readonly INotificationService _notificationService;
    private readonly CompositeDisposable _disposables = [];
    private readonly ReadOnlyObservableCollection<IQueueItem> _queuedItems;
    private bool _suppressAutoEnqueue;
    private TimeSpan _currentItemElapsed;
    private TimeSpan _currentItemTotalDuration;

    public ReadOnlyObservableCollection<IQueueItem> QueuedItems => _queuedItems;

    [Reactive] public partial IQueueItem? SelectedItem { get; set; }

    private IObservable<bool> CanEditSelected =>
        this.WhenAnyValue(x => x.SelectedItem).Select(item => TrackOf(item) is not null);

    /// <summary>
    /// Opens the edit dialog for the selected item's track, same as the catalog's right-click.
    /// </summary>
    /// <remarks>
    /// The queued item keeps the snapshot it was enqueued with; the correction shows in the
    /// library at once and on this queue entry the next time the track is enqueued.
    /// </remarks>
    [ReactiveCommand(CanExecute = nameof(CanEditSelected))]
    private async Task EditSelectedTrackAsync()
    {
        if (TrackOf(SelectedItem) is { } track)
        {
            await _trackEditor.EditAsync(track);
        }
    }

    private static Domain.Models.Tracks.Track? TrackOf(IQueueItem? item) => item switch
    {
        TrackQueueItem track => track.Track,
        AutoTrackQueueItem auto => auto.TrackQueueItem.Track,
        _ => null
    };
    [Reactive] public partial string ItemCountText { get; set; }
    [Reactive] public partial string FinishTimeText { get; set; }

    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool HasItems { get; set; }

    /// <summary>
    /// Whether there is an end-of-the-night file to offer, and an evening left to end with it.
    /// </summary>
    /// <remarks>
    /// An application that has never been told what the sound is has no business offering to play
    /// it, and neither has one whose file has since moved: the button goes quiet rather than failing
    /// at the moment somebody presses it in front of a room.
    /// </remarks>
    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool IsEndOfNightAvailable { get; set; }

    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool IsSelectedMovableUp { get; set; }
    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool IsSelectedMovableDown { get; set; }

    private IObservable<bool> CanRemoveSelected =>
        this.WhenAnyValue(x => x.SelectedItem)
            .Select(item => item is not null and not AutoTrackQueueItem);

    private IObservable<bool> CanEnqueueEndOfNight => this.WhenAnyValue(x => x.IsEndOfNightAvailable);

    private IObservable<bool> CanMoveSelectedUp => this.WhenAnyValue(x => x.IsSelectedMovableUp);
    private IObservable<bool> CanMoveSelectedDown => this.WhenAnyValue(x => x.IsSelectedMovableDown);

    private IObservable<bool> CanClearQueue => this.WhenAnyValue(x => x.HasItems);

    [ReactiveCommand]
    private void QueueRandomTrack()
    {
        var scope = GetPoolScope();
        var track = _randomTrackService.PickRandomTrack(
            scope, _settingsStore.Current.AllowDuplicateTracksInQueue);
        if (track is not null)
        {
            var result = _queueService.Enqueue(new TrackQueueItem(track, RandomlyAdded: true));
            if (!result.Allowed)
            {
                _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
            }
        }
        else
        {
            _notificationService.Show("No tracks available for random selection", NotificationSeverity.Warning);
        }
    }

    private RandomSelectionScope GetPoolScope() => _dancePool.Scope;

    [ReactiveCommand]
    private void EnqueueStop()
    {
        var result = _queueService.Enqueue(new StopQueueItem());
        if (!result.Allowed)
        {
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    [ReactiveCommand]
    private void EnqueueDelay()
    {
        var result = _queueService.Enqueue(new DelayQueueItem(TimeSpan.FromSeconds(_settingsStore.Current.DelaySeconds)));
        if (!result.Allowed)
        {
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    /// <summary>Declares the evening over, which is the last thing that goes into the queue.</summary>
    [ReactiveCommand(CanExecute = nameof(CanEnqueueEndOfNight))]
    private void EnqueueEndOfNight()
    {
        // The file was there when the button was last offered; if it has gone since, say so rather
        // than quietly doing nothing.
        if (_endOfNightAudio.Create() is not { } item)
        {
            _notificationService.Show(UiStrings.Queue_EndOfNightMissing, NotificationSeverity.Warning);
            UpdateEndOfNightAvailability();
            return;
        }

        var result = _queueService.Enqueue(item);
        if (!result.Allowed)
        {
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected() => DeleteSelectedItem();

    [ReactiveCommand(CanExecute = nameof(CanMoveSelectedUp))]
    public void MoveSelectedUp()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var item = SelectedItem;
        var index = IndexOfSelected();
        if (index > 0)
        {
            MoveItem(index, index - 1);
            RxSchedulers.MainThreadScheduler.Schedule(item, (_, i) => { SelectedItem = i; return Disposable.Empty; });
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanMoveSelectedDown))]
    public void MoveSelectedDown()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var item = SelectedItem;
        var index = IndexOfSelected();
        if (index >= 0 && index < _queuedItems.Count - 1)
        {
            MoveItem(index, index + 1);
            RxSchedulers.MainThreadScheduler.Schedule(item, (_, i) => { SelectedItem = i; return Disposable.Empty; });
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanClearQueue))]
    private async Task ClearQueue()
    {
        if (!await _confirmationService.ConfirmAsync(UiStrings.QueueToolbar_ClearQueueTitle, UiStrings.QueueToolbar_ClearQueueMessage, UiStrings.QueueToolbar_ClearButton,
                UiStrings.QueueToolbar_CancelButton))
        {
            return;
        }

        _queueService.Clear();
    }

    public void EnqueueMessage(string message, TimeSpan? duration)
    {
        var result = _queueService.Enqueue(new MessageQueueItem(message, duration));
        if (!result.Allowed)
        {
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    public QueueViewModel(
        IQueueService queueService,
        IQueueConsumptionService consumptionService,
        ISettingsStore settingsStore,
        IRandomTrackService randomTrackService,
        IDancePool dancePool,
        IConfirmationService confirmationService,
        INotificationService notificationService,
        IEndOfNightAudio endOfNightAudio,
        TrackEditorService trackEditor)
    {
        _trackEditor = trackEditor;
        _queueService = queueService;
        _endOfNightAudio = endOfNightAudio;
        _consumptionService = consumptionService;
        _settingsStore = settingsStore;
        _randomTrackService = randomTrackService;
        _dancePool = dancePool;
        _confirmationService = confirmationService;
        _notificationService = notificationService;
        ItemCountText = UiStrings.Queue_Empty;
        FinishTimeText = "";

        queueService.Connect()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out _queuedItems)
            .Subscribe(_ =>
            {
                HasItems = _queuedItems.Any(i => i is not AutoTrackQueueItem);
                UpdateItemCountText();
                UpdateMoveStates();

                if (!_suppressAutoEnqueue)
                {
                    TryAutoEnqueue();
                }
            })
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(_ => UpdateMoveStates())
            .DisposeWith(_disposables);

        settingsStore.Observe()
            .Select(s => s.AutoQueueRandomTrack)
            .DistinctUntilChanged()
            .Where(enabled => enabled)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => TryAutoEnqueue())
            .DisposeWith(_disposables);

        consumptionService.WhenCurrentItemChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(item =>
            {
                if (item is null)
                {
                    _queueService.RemoveWhere(i => i is AutoTrackQueueItem);
                }
                else
                {
                    TryAutoEnqueue();
                }
            })
            .DisposeWith(_disposables);

        consumptionService.WhenElapsedChanged
            .Subscribe(e => _currentItemElapsed = e)
            .DisposeWith(_disposables);
        consumptionService.WhenTotalDurationChanged
            .Subscribe(d => _currentItemTotalDuration = d)
            .DisposeWith(_disposables);

        var queueChanged = queueService.Connect().Select(_ => Unit.Default);
        var currentItemChanged = consumptionService.WhenCurrentItemChanged.Select(_ => Unit.Default);
        var totalDurationChanged = consumptionService.WhenTotalDurationChanged.Select(_ => Unit.Default);
        var elapsedTick = consumptionService.WhenElapsedChanged
            .Sample(TimeSpan.FromSeconds(1))
            .Select(_ => Unit.Default);
        var minuteTimer = Observable.Interval(TimeSpan.FromSeconds(30))
            .Select(_ => Unit.Default);

        Observable.Merge(queueChanged, currentItemChanged, totalDurationChanged, elapsedTick, minuteTimer)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateFinishTimeText())
            .DisposeWith(_disposables);

        // The file can move between settings changes, so the timer rechecks it rather than trusting
        // an answer from earlier in the evening.
        var settingsChanged = settingsStore.Observe().Select(_ => Unit.Default);
        UpdateEndOfNightAvailability();
        Observable.Merge(queueChanged, currentItemChanged, settingsChanged, minuteTimer)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateEndOfNightAvailability())
            .DisposeWith(_disposables);
    }

    private void UpdateEndOfNightAvailability() =>
        IsEndOfNightAvailable = _endOfNightAudio.IsAvailable && !HasEveningEnded;

    /// <summary>Whether the evening has been called, whether it is queued or already playing.</summary>
    private bool HasEveningEnded =>
        _queuedItems.Any(i => i is EndOfNightQueueItem)
        || _consumptionService.CurrentItem is EndOfNightQueueItem;

    private void UpdateMoveStates()
    {
        if (SelectedItem is null || IsPinnedToTail(SelectedItem))
        {
            IsSelectedMovableUp = false;
            IsSelectedMovableDown = false;
            return;
        }

        var index = IndexOfSelected();
        var lastMovable = LastRequestIndex();
        IsSelectedMovableUp = index > 0;
        IsSelectedMovableDown = index >= 0 && index < lastMovable;
    }

    // The auto-track and the end of the night are pinned to the bottom, so a request cannot be
    // moved below either of them.
    private int LastRequestIndex()
    {
        for (var i = _queuedItems.Count - 1; i >= 0; i--)
        {
            if (!IsPinnedToTail(_queuedItems[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsPinnedToTail(IQueueItem item) => item is AutoTrackQueueItem or EndOfNightQueueItem;

    // Queue items are records with value equality and duplicates are allowed
    // (e.g. two StopQueueItems), so the selected item must be located by
    // reference identity: IndexOf would return the first equal instance.
    private int IndexOfSelected()
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return -1;
        }

        for (var i = 0; i < _queuedItems.Count; i++)
        {
            if (ReferenceEquals(_queuedItems[i], selected))
            {
                return i;
            }
        }

        return -1;
    }

    public void DeleteSelectedItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var index = IndexOfSelected();
        if (index >= 0)
        {
            _queueService.RemoveAt(index);
        }
    }

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _queuedItems.Count)
        {
            return;
        }

        if (newIndex < 0 || newIndex >= _queuedItems.Count)
        {
            return;
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        _queueService.Move(oldIndex, newIndex);
    }

    public void PinAutoTrack(AutoTrackQueueItem item)
    {
        var index = _queuedItems.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        _suppressAutoEnqueue = true;
        try
        {
            // The auto-track has to come out first or it blocks its own pin as a duplicate, but
            // the pin can still be refused (a full queue). Put the same one back when that
            // happens, so a refused pin leaves the queue exactly as it was rather than silently
            // rerolling the track.
            _queueService.RemoveWhere(i => i is AutoTrackQueueItem);
            var result = _queueService.InsertAt(index, item.TrackQueueItem with
            {
                RandomlyAdded = true
            });

            if (!result.Allowed)
            {
                _queueService.Enqueue(item);
                _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
            }
        }
        finally
        {
            _suppressAutoEnqueue = false;
        }
    }

    private void TryAutoEnqueue()
    {
        if (!_settingsStore.Current.AutoQueueRandomTrack)
        {
            return;
        }

        // One auto-track at a time, and only once something is playing: with nothing playing the
        // queue is genuinely empty rather than waiting for a follow-up track.
        if (_queuedItems.Any(i => i is AutoTrackQueueItem))
        {
            return;
        }

        if (_consumptionService.CurrentItem is null)
        {
            return;
        }

        // The evening has been ended, so the machine does not extend it.
        if (HasEveningEnded)
        {
            return;
        }

        var scope = GetPoolScope();
        var track = _randomTrackService.PickRandomTrack(scope, _settingsStore.Current.AllowDuplicateTracksInQueue);
        if (track is null)
        {
            return;
        }

        var result = _queueService.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(track, RandomlyAdded: true)));
        if (result.Denial is QueueDenial.Cutoff)
        {
            EndTheNightAtCutoff();
        }
    }

    /// <summary>
    /// The cutoff has just refused the track that would have followed, so this is the last track it
    /// allowed and the evening ends behind it.
    /// </summary>
    private void EndTheNightAtCutoff()
    {
        if (!_settingsStore.Current.PlayEndOfNightAtCutoff)
        {
            return;
        }

        if (_endOfNightAudio.Create() is not { } item)
        {
            return;
        }

        _queueService.Enqueue(item);
    }

    public void RefreshAutoTrack()
    {
        var currentFile = _queuedItems.OfType<AutoTrackQueueItem>().FirstOrDefault()?.TrackQueueItem.Track.FileInfo
            .FullName;
        if (currentFile is null)
        {
            return;
        }

        _suppressAutoEnqueue = true;
        _queueService.RemoveWhere(i => i is AutoTrackQueueItem);
        _suppressAutoEnqueue = false;

        var scope = GetPoolScope();
        var allowDuplicates = _settingsStore.Current.AllowDuplicateTracksInQueue;
        var track = _randomTrackService.PickRandomTrack(scope, allowDuplicates);

        // If we got the same track, retry once
        if (track is not null && track.FileInfo.FullName == currentFile)
        {
            track = _randomTrackService.PickRandomTrack(scope, allowDuplicates) ?? track;
        }

        if (track is not null)
        {
            _queueService.Enqueue(new AutoTrackQueueItem(new TrackQueueItem(track, RandomlyAdded: true)));
        }
    }

    private void UpdateItemCountText()
    {
        ItemCountText = _queuedItems.Count == 0
            ? UiStrings.Queue_Empty
            : _queuedItems.Count == 1
                ? string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_ItemCount, _queuedItems.Count)
                : string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_ItemCountPlural, _queuedItems.Count);
    }

    private void UpdateFinishTimeText()
    {
        if (_consumptionService.CurrentItem is null && _queuedItems.Count == 0)
        {
            FinishTimeText = "";
            return;
        }

        // Remaining time of currently playing item
        var currentRemaining = TimeSpan.Zero;
        if (_consumptionService.CurrentItem is not null)
        {
            currentRemaining = _currentItemTotalDuration > _currentItemElapsed
                ? _currentItemTotalDuration - _currentItemElapsed
                : TimeSpan.Zero;
        }

        // Sum queue durations, stopping at halt points
        var queueDuration = TimeSpan.Zero;
        var halts = false;
        foreach (var item in _queuedItems)
        {
            if (item is StopQueueItem or MessageQueueItem { Duration: null })
            {
                halts = true;
                break;
            }

            queueDuration += item.Duration ?? TimeSpan.Zero;
        }

        var settings = _settingsStore.Current;
        var finishTime = DateTime.Now + currentRemaining + queueDuration;
        string text;
        if (halts)
        {
            text = string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_PlaylistHaltsAt,
                finishTime.ToString("HH:mm", CultureInfo.CurrentCulture));

            // Say when the cutoff is not being applied, rather than leaving the user to wonder why a
            // request went through: past a halt there is no end time to judge it against.
            if (settings.QueueCutoffEnabled)
            {
                text += $" \u2014 {UiStrings.Queue_CutoffPaused}";
            }
        }
        // The evening being over is knowable in exactly the way an open-ended one is not: nothing
        // more goes in, so what is queued is the whole of what is left.
        else if (!settings.AutoQueueRandomTrack || HasEveningEnded)
        {
            text = string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_PlaylistFinishesAt,
                finishTime.ToString("HH:mm", CultureInfo.CurrentCulture));
        }
        else if (settings.QueueCutoffEnabled)
        {
            // The auto-queue keeps refilling until the cutoff refuses the next track, so the cutoff
            // is the end of the evening rather than the moment the current contents run out.
            text = string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_PlaylistWindsDownAt,
                (DateTime.Today + settings.QueueCutoff).ToString("HH:mm", CultureInfo.CurrentCulture));
        }
        else
        {
            // A projected time here would only ever describe the next few minutes: it moves forward
            // every time a track ends, and it reads as an answer while never being one.
            text = UiStrings.Queue_PlaylistOpenEnded;
        }

        FinishTimeText = text;
    }

    public void Dispose() => _disposables.Dispose();
}
