using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Presentation;

public sealed partial class PresentationDisplayViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    // Current item properties
    [Reactive] public partial string CurrentDance { get; set; }
    [Reactive] public partial string CurrentArtist { get; set; }
    [Reactive] public partial string CurrentTitle { get; set; }
    [Reactive] public partial bool HasCurrentItem { get; set; }
    [Reactive] public partial bool IsMessageMode { get; set; }
    [Reactive] public partial TimeSpan Duration { get; set; }
    [Reactive] public partial TimeSpan Progress { get; set; }
    [Reactive] public partial string CurrentTimeLeft { get; set; }

    // Next item properties
    [Reactive] public partial string NextDance { get; set; }
    [Reactive] public partial string NextArtist { get; set; }
    [Reactive] public partial string NextTitle { get; set; }
    [Reactive] public partial bool HasNextItem { get; set; }

    public PresentationDisplayViewModel(IQueueConsumptionService consumptionService, IQueueService queueService)
    {
        CurrentDance = "";
        CurrentArtist = "";
        CurrentTitle = "";
        NextDance = "";
        NextArtist = "";
        NextTitle = "";
        CurrentTimeLeft = "";

        consumptionService.WhenCurrentItemChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(OnCurrentItemChanged)
            .DisposeWith(_disposables);

        consumptionService.WhenElapsedChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(elapsed =>
            {
                Progress = elapsed;
                CurrentTimeLeft = FormatTimeLeft(Duration - elapsed);
            })
            .DisposeWith(_disposables);

        consumptionService.WhenTotalDurationChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(dur =>
            {
                Duration = dur;
                CurrentTimeLeft = FormatTimeLeft(dur - Progress);
            })
            .DisposeWith(_disposables);

        queueService.Connect()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateNextItem(queueService))
            .DisposeWith(_disposables);
    }

    private void OnCurrentItemChanged(IQueueItem? item)
    {
        HasCurrentItem = item != null;

        if (item == null)
        {
            ClearCurrentState();
            return;
        }

        switch (item)
        {
            case AutoTrackQueueItem auto:
                SetCurrentTrack(auto.TrackQueueItem);
                break;
            case TrackQueueItem track:
                SetCurrentTrack(track);
                break;
            case MessageQueueItem message:
                IsMessageMode = true;
                CurrentDance = message.Description;
                CurrentArtist = "";
                CurrentTitle = "";
                break;
            case DelayQueueItem:
                IsMessageMode = false;
                CurrentDance = UiStrings.Presentation_Delay;
                CurrentArtist = "";
                CurrentTitle = "";
                break;
            case StopQueueItem:
                IsMessageMode = false;
                CurrentDance = UiStrings.Presentation_Stop;
                CurrentArtist = "";
                CurrentTitle = "";
                break;
            default:
                break;
        }
    }

    private void SetCurrentTrack(TrackQueueItem trackItem)
    {
        IsMessageMode = false;
        CurrentDance = trackItem.Track.Dance;
        CurrentArtist = trackItem.Track.Artist;
        CurrentTitle = trackItem.Track.Title;
    }

    private void ClearCurrentState()
    {
        IsMessageMode = false;
        CurrentDance = "";
        CurrentArtist = "";
        CurrentTitle = "";
        Progress = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        CurrentTimeLeft = "";
    }

    private void UpdateNextItem(IQueueService queueService)
    {
        var next = queueService.Peek();
        HasNextItem = next != null;

        if (next == null)
        {
            NextDance = "";
            NextArtist = "";
            NextTitle = "";
            return;
        }

        switch (next)
        {
            case AutoTrackQueueItem auto:
                SetNextTrack(auto.TrackQueueItem);
                break;
            case TrackQueueItem track:
                SetNextTrack(track);
                break;
            case MessageQueueItem message:
                NextDance = UiStrings.Presentation_Message;
                NextArtist = message.Description;
                NextTitle = "";
                break;
            case DelayQueueItem:
                NextDance = UiStrings.Presentation_Delay;
                NextArtist = "";
                NextTitle = "";
                break;
            case StopQueueItem:
                NextDance = UiStrings.Presentation_Stop;
                NextArtist = "";
                NextTitle = "";
                break;
            default:
                break;
        }
    }

    private void SetNextTrack(TrackQueueItem trackItem)
    {
        NextDance = trackItem.Track.Dance;
        NextArtist = trackItem.Track.Artist;
        NextTitle = trackItem.Track.Title;
    }

    private static string FormatTimeLeft(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";
    }

    public void Dispose() => _disposables.Dispose();
}
