using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Playback;

public sealed partial class PlaybackViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueConsumptionService _consumptionService;
    private readonly IQueueService _queueService;
    private readonly IConfirmationService _confirmationService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsStore _settingsStore;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>What is on screen, so it can be written again when the templates change.</summary>
    private IQueueItem? _showing;

    /// <summary>The question on screen, so the item it is about can withdraw it when it ends.</summary>
    private CancellationTokenSource? _pendingConfirmation;

    [Reactive] public partial string DanceName { get; set; }
    [Reactive] public partial bool IsMessageMode { get; set; }
    [Reactive] public partial bool HasTrack { get; set; }
    /// <summary>The line under the big one, written the way the user asked for it.</summary>
    [Reactive] public partial string TrackLine { get; set; }
    [Reactive] public partial string CurrentTime { get; set; }
    [Reactive] public partial string TotalTime { get; set; }
    [Reactive] public partial double Duration { get; set; }
    [Reactive] public partial double Progress { get; set; }
    [Reactive] public partial bool IsPlaying { get; set; }
    [Reactive] public partial bool ShowNextIcon { get; set; }
    [Reactive] public partial bool HasCurrentItem { get; set; }
    /// <summary>Whether what is on is a file playing, which is what these three buttons act on.</summary>
    [Reactive] public partial bool HasAudioItem { get; set; }
    [Reactive] public partial bool IsAudioUnavailable { get; set; }

    /// <summary>Play holds what is on, and with nothing on at all it starts the evening.</summary>
    private IObservable<bool> CanPlayPause =>
        this.WhenAnyValue(
            x => x.HasAudioItem,
            x => x.HasCurrentItem,
            x => x.ShowNextIcon,
            x => x.IsAudioUnavailable,
            (audio, current, next, unavailable) => (audio || (!current && next)) && !unavailable);

    private IObservable<bool> CanRestart =>
        this.WhenAnyValue(x => x.HasAudioItem, x => x.IsAudioUnavailable, (has, unavailable) => has && !unavailable);

    private IObservable<bool> CanNextOrClear =>
        this.WhenAnyValue(x => x.ShowNextIcon, x => x.HasCurrentItem, (next, current) => next || current);

    private IObservable<bool> CanSeek =>
        this.WhenAnyValue(x => x.HasAudioItem, x => x.IsAudioUnavailable, (has, unavailable) => has && !unavailable);

    [ReactiveCommand(CanExecute = nameof(CanPlayPause))]
    private async Task PlayPause()
    {
        // Enabled from a state that can have passed between the button being drawn and the click
        // landing: a dance that ran out leaves nothing to hold, and the DJ is told rather than left
        // pressing again.
        if (!await _consumptionService.PlayPauseAsync())
        {
            ReportTooLate();
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanRestart))]
    private async Task Restart()
    {
        if (!await ConfirmForAsync(_consumptionService.CurrentItem, UiStrings.Playback_RestartTitle, UiStrings.Playback_RestartMessage, UiStrings.Playback_RestartButton))
        {
            return;
        }

        if (!await _consumptionService.RestartAsync())
        {
            ReportTooLate();
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanNextOrClear))]
    private async Task NextOrClear()
    {
        var asked = _consumptionService.CurrentItem;

        var confirmed = _queueService.Count > 0
            ? await ConfirmForAsync(asked, UiStrings.Playback_SkipTitle, UiStrings.Playback_SkipMessage, UiStrings.Playback_SkipButton)
            : await ConfirmForAsync(asked, UiStrings.Playback_ClearTitle, UiStrings.Playback_ClearMessage, UiStrings.Playback_ClearButton);

        if (!confirmed)
        {
            return;
        }

        if (_queueService.Count > 0)
        {
            // The item is named, so an evening that moved on between the answer and the gate drops
            // the skip rather than taking the dance that had just started.
            if (!await _consumptionService.AdvanceAsync(asked))
            {
                ReportTooLate();
            }
        }
        else
        {
            await _consumptionService.ClearAsync();
        }
    }

    /// <summary>Asks about <paramref name="asked" />, and says no when the answer came too late.</summary>
    /// <remarks>
    /// A question is about whatever was playing when it was asked. Answered after that dance ended,
    /// "yes" skips the one that followed it, restarts it, or seeks it to a position measured against
    /// a length that was never its own. The question is withdrawn along with its dance for the same
    /// reason. Either way the DJ is told: a button pressed and confirmed that then does nothing is a
    /// button that gets pressed again, harder, with a room watching.
    /// </remarks>
    private async Task<bool> ConfirmForAsync(IQueueItem? asked, string title, string message, string confirmText)
    {
        if (!HasCurrentItem || !_settingsStore.Current.RequirePlaybackConfirmation)
        {
            return true;
        }

        using var withdrawal = new CancellationTokenSource();
        _pendingConfirmation = withdrawal;

        bool confirmed;
        try
        {
            confirmed = await _confirmationService.ConfirmAsync(title, message, confirmText, UiStrings.Playback_CancelButton, withdrawal.Token);
        }
        finally
        {
            _pendingConfirmation = null;
        }

        if (!withdrawal.IsCancellationRequested && ReferenceEquals(_consumptionService.CurrentItem, asked))
        {
            return confirmed;
        }

        if (confirmed || withdrawal.IsCancellationRequested)
        {
            ReportTooLate();
        }

        return false;
    }

    private void ReportTooLate() =>
        _notificationService.Show(UiStrings.Playback_AnswerTooLate, NotificationSeverity.Warning);

    public PlaybackViewModel(IQueueConsumptionService consumptionService, IQueueService queueService, IConfirmationService confirmationService, INotificationService notificationService, ISettingsStore settingsStore, IAudioPlaybackService audioPlaybackService)
    {
        _consumptionService = consumptionService;
        _queueService = queueService;
        _confirmationService = confirmationService;
        _notificationService = notificationService;
        _settingsStore = settingsStore;
        DanceName = "";
        TrackLine = "";
        CurrentTime = "0:00";
        TotalTime = "0:00";

        audioPlaybackService.WhenAvailabilityChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(available => IsAudioUnavailable = !available)
            .DisposeWith(_disposables);

        consumptionService.WhenCurrentItemChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(item =>
            {
                _pendingConfirmation?.Cancel();
                _showing = item;
                OnCurrentItemChanged(item);
            })
            .DisposeWith(_disposables);

        // A template edited while something is playing is on screen at once. These lines are
        // written when an item starts, so without this the change would wait for the next dance.
        settingsStore.Observe()
            .Select(settings => settings.DisplayTemplates)
            .DistinctUntilChanged()
            .Skip(1)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => OnCurrentItemChanged(_showing))
            .DisposeWith(_disposables);

        consumptionService.WhenElapsedChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(elapsed =>
            {
                Progress = elapsed.TotalSeconds;
                CurrentTime = FormatTime(elapsed);
            })
            .DisposeWith(_disposables);

        consumptionService.WhenTotalDurationChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(dur =>
            {
                Duration = dur.TotalSeconds;
                TotalTime = FormatTime(dur);
            })
            .DisposeWith(_disposables);

        consumptionService.WhenIsPlayingChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(playing => IsPlaying = playing)
            .DisposeWith(_disposables);

        queueService.Connect()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => ShowNextIcon = _queueService.Count > 0)
            .DisposeWith(_disposables);
    }

    private void OnCurrentItemChanged(IQueueItem? item)
    {
        HasCurrentItem = item != null;
        HasAudioItem = AudioItems.IsAudio(item);

        if (item == null)
        {
            ClearState();
            return;
        }

        switch (item)
        {
            case AutoTrackQueueItem auto:
                SetTrackDisplay(auto.TrackQueueItem);
                break;
            case TrackQueueItem track:
                SetTrackDisplay(track);
                break;
            case MessageQueueItem message:
                IsMessageMode = true;
                HasTrack = false;
                DanceName = message.Description;
                TrackLine = "";
                break;
            case DelayQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_Delay;
                TrackLine = "";
                break;
            case GapQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_Gap;
                TrackLine = "";
                break;
            case StopQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_Stop;
                TrackLine = "";
                break;
            case EndOfNightQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_EndOfNight;
                TrackLine = "";
                break;
            default:
                break;
        }
    }

    private void SetTrackDisplay(TrackQueueItem trackItem)
    {
        IsMessageMode = false;
        HasTrack = true;
        var templates = _settingsStore.Current.DisplayTemplates;
        DanceName = TrackTextTemplate.Render(templates.NowPlayingPrimary, trackItem.Track);
        TrackLine = TrackTextTemplate.Render(templates.NowPlayingSecondary, trackItem.Track);
    }

    private void ClearState()
    {
        IsPlaying = false;
        HasTrack = false;
        IsMessageMode = false;
        DanceName = "";
        TrackLine = "";
        Progress = 0;
        Duration = 0;
        CurrentTime = "0:00";
        TotalTime = "0:00";
    }

    private static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalMinutes}:{time.Seconds:D2}";

    /// <summary>
    /// Moves playback to a position on the bar, one at a time.
    /// </summary>
    /// <remarks>
    /// A command like every other confirmed action, because a command will not run again while it
    /// is still running. Started as a bare call it could: a second click while the confirmation was
    /// up raised a second confirmation, each one holding the position it was made with, and working
    /// through the stack walked the track forward instead of landing on the spot that was clicked.
    /// </remarks>
    [ReactiveCommand(CanExecute = nameof(CanSeek))]
    private async Task Seek(TimeSpan position)
    {
        if (!await ConfirmForAsync(_consumptionService.CurrentItem, UiStrings.Playback_SeekTitle, UiStrings.Playback_SeekMessage, UiStrings.Playback_SeekButton))
        {
            return;
        }

        if (!await _consumptionService.SeekAsync(position))
        {
            ReportTooLate();
        }
    }

    public void Dispose() => _disposables.Dispose();
}
