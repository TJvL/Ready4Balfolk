using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Audio;
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
    private readonly ISettingsStore _settingsStore;
    private readonly CompositeDisposable _disposables = [];

    [Reactive] public partial string DanceName { get; set; }
    [Reactive] public partial bool IsMessageMode { get; set; }
    [Reactive] public partial bool HasTrack { get; set; }
    [Reactive] public partial string ArtistName { get; set; }
    [Reactive] public partial string TrackTitle { get; set; }
    [Reactive] public partial string CurrentTime { get; set; }
    [Reactive] public partial string TotalTime { get; set; }
    [Reactive] public partial double Duration { get; set; }
    [Reactive] public partial double Progress { get; set; }
    [Reactive] public partial bool IsPlaying { get; set; }
    [Reactive] public partial bool ShowNextIcon { get; set; }
    [Reactive] public partial bool HasCurrentItem { get; set; }
    [Reactive] public partial bool IsAudioUnavailable { get; set; }

    private IObservable<bool> CanPlayPause =>
        this.WhenAnyValue(x => x.HasTrack, x => x.IsAudioUnavailable, (has, unavailable) => has && !unavailable);

    private IObservable<bool> CanRestart =>
        this.WhenAnyValue(x => x.HasTrack, x => x.IsAudioUnavailable, (has, unavailable) => has && !unavailable);

    private IObservable<bool> CanNextOrClear =>
        this.WhenAnyValue(x => x.ShowNextIcon, x => x.HasCurrentItem, (next, current) => next || current);

    [ReactiveCommand(CanExecute = nameof(CanPlayPause))]
    private async Task PlayPause() => await _consumptionService.PlayPauseAsync();

    [ReactiveCommand(CanExecute = nameof(CanRestart))]
    private async Task Restart()
    {
        if (HasCurrentItem && _settingsStore.Current.RequirePlaybackConfirmation &&
            !await _confirmationService.ConfirmAsync(UiStrings.Playback_RestartTitle, UiStrings.Playback_RestartMessage, UiStrings.Playback_RestartButton, UiStrings.Playback_CancelButton))
        {
            return;
        }

        await _consumptionService.RestartAsync();
    }

    [ReactiveCommand(CanExecute = nameof(CanNextOrClear))]
    private async Task NextOrClear()
    {
        if (HasCurrentItem && _settingsStore.Current.RequirePlaybackConfirmation)
        {
            if (_queueService.Count > 0)
            {
                if (!await _confirmationService.ConfirmAsync(UiStrings.Playback_SkipTitle, UiStrings.Playback_SkipMessage, UiStrings.Playback_SkipButton, UiStrings.Playback_CancelButton))
                {
                    return;
                }
            }
            else
            {
                if (!await _confirmationService.ConfirmAsync(UiStrings.Playback_ClearTitle, UiStrings.Playback_ClearMessage, UiStrings.Playback_ClearButton, UiStrings.Playback_CancelButton))
                {
                    return;
                }
            }
        }

        if (_queueService.Count > 0)
        {
            await _consumptionService.AdvanceAsync();
        }
        else
        {
            await _consumptionService.ClearAsync();
        }
    }

    public PlaybackViewModel(IQueueConsumptionService consumptionService, IQueueService queueService, IConfirmationService confirmationService, ISettingsStore settingsStore, IAudioPlaybackService audioPlaybackService)
    {
        _consumptionService = consumptionService;
        _queueService = queueService;
        _confirmationService = confirmationService;
        _settingsStore = settingsStore;
        DanceName = "";
        ArtistName = "";
        TrackTitle = "";
        CurrentTime = "0:00";
        TotalTime = "0:00";

        audioPlaybackService.WhenAvailabilityChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(available => IsAudioUnavailable = !available)
            .DisposeWith(_disposables);

        consumptionService.WhenCurrentItemChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(OnCurrentItemChanged)
            .DisposeWith(_disposables);

        consumptionService.WhenElapsedChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(elapsed =>
            {
                Progress = elapsed.TotalSeconds;
                CurrentTime = FormatTime(elapsed);
            })
            .DisposeWith(_disposables);

        consumptionService.WhenTotalDurationChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(dur =>
            {
                Duration = dur.TotalSeconds;
                TotalTime = FormatTime(dur);
            })
            .DisposeWith(_disposables);

        consumptionService.WhenIsPlayingChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(playing => IsPlaying = playing)
            .DisposeWith(_disposables);

        queueService.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ShowNextIcon = _queueService.Count > 0)
            .DisposeWith(_disposables);
    }

    private void OnCurrentItemChanged(IQueueItem? item)
    {
        HasCurrentItem = item != null;

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
                ArtistName = "";
                TrackTitle = "";
                break;
            case DelayQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_Delay;
                ArtistName = "";
                TrackTitle = "";
                break;
            case StopQueueItem:
                IsMessageMode = false;
                HasTrack = false;
                DanceName = UiStrings.Playback_Stop;
                ArtistName = "";
                TrackTitle = "";
                break;
            default:
                break;
        }
    }

    private void SetTrackDisplay(TrackQueueItem trackItem)
    {
        IsMessageMode = false;
        HasTrack = true;
        DanceName = trackItem.Track.Dance;
        ArtistName = trackItem.Track.Artist;
        TrackTitle = trackItem.Track.Title;
    }

    private void ClearState()
    {
        IsPlaying = false;
        HasTrack = false;
        IsMessageMode = false;
        DanceName = "";
        ArtistName = "";
        TrackTitle = "";
        Progress = 0;
        Duration = 0;
        CurrentTime = "0:00";
        TotalTime = "0:00";
    }

    private static string FormatTime(TimeSpan time)
        => $"{(int)time.TotalMinutes}:{time.Seconds:D2}";

    public void Dispose() => _disposables.Dispose();
}
