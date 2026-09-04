using System;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Playback;

public partial class PlaybackView : ReactiveUserControl<PlaybackViewModel>
{
    private IDisposable? _trackInfoScroll;

    /// <summary>Whether the press that is in flight started on the bar, so a drag from elsewhere
    /// does not land as a seek when the button comes up over it.</summary>
    private bool _pressedOnTheBar;

    public PlaybackView()
    {
        InitializeComponent();

        PlaybackProgressBar.PointerPressed += OnProgressBarPointerPressed;
        PlaybackProgressBar.PointerReleased += OnProgressBarPointerReleased;
        PlaybackProgressBar.PointerCaptureLost += (_, _) => _pressedOnTheBar = false;

        TrackInfoCanvas.GetObservable(BoundsProperty)
            .CombineLatest(TrackInfoPanel.GetObservable(BoundsProperty))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateTrackInfoScroll());
    }

    /// <summary>
    /// The press is only noted. What it starts happens when the button comes back up.
    /// </summary>
    /// <remarks>
    /// A confirmation raised while the mouse is still down is a window opened underneath a pointer
    /// the old one has hold of: it comes up without the pointer, so it does not light up under the
    /// cursor and the first click on it only wakes it. Every other confirmed action comes off a
    /// button's Click, which is the release, and none of them behave that way.
    /// </remarks>
    private void OnProgressBarPointerPressed(object? sender, PointerPressedEventArgs e) =>
        _pressedOnTheBar = e.GetCurrentPoint(PlaybackProgressBar).Properties.IsLeftButtonPressed;

    private void OnProgressBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasPressed = _pressedOnTheBar;
        _pressedOnTheBar = false;

        if (!wasPressed || ViewModel is not { HasTrack: true, Duration: > 0 } vm)
        {
            return;
        }

        var x = e.GetPosition(PlaybackProgressBar).X;
        var ratio = Math.Clamp(x / PlaybackProgressBar.Bounds.Width, 0, 1);
        var target = TimeSpan.FromSeconds(ratio * vm.Duration);

        // Asked rather than fired: while the confirmation for the last click is still up the
        // command will not run, and a click that arrives anyway is dropped instead of stacking a
        // second dialog behind the first.
        var seek = (ICommand)vm.SeekCommand;
        if (seek.CanExecute(target))
        {
            seek.Execute(target);
        }
    }

    private void UpdateTrackInfoScroll()
    {
        _trackInfoScroll?.Dispose();
        _trackInfoScroll = null;

        var canvasWidth = TrackInfoCanvas.Bounds.Width;
        var panelWidth = TrackInfoPanel.Bounds.Width;

        if (canvasWidth <= 0 || panelWidth <= 0)
        {
            return;
        }

        if (panelWidth <= canvasWidth)
        {
            Canvas.SetLeft(TrackInfoPanel, (canvasWidth - panelWidth) / 2);
            return;
        }

        // Overflows: scroll back and forth
        var overflow = panelWidth - canvasWidth;
        const double scrollSpeed = 50.0; // px/sec
        const double pauseSec = 2.0;
        var scrollSec = overflow / scrollSpeed;
        var cycleSec = pauseSec + scrollSec + pauseSec + scrollSec;

        var startTime = DateTimeOffset.UtcNow;

        _trackInfoScroll = Observable.Interval(TimeSpan.FromMilliseconds(16))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ =>
            {
                var t = (DateTimeOffset.UtcNow - startTime).TotalSeconds % cycleSec;

                var left = t < pauseSec
                    ? 0
                    : t < pauseSec + scrollSec
                        ? -overflow * ((t - pauseSec) / scrollSec)
                        : t < pauseSec + scrollSec + pauseSec
                            ? -overflow
                            : -overflow * (1 - ((t - pauseSec - scrollSec - pauseSec) / scrollSec));

                Canvas.SetLeft(TrackInfoPanel, left);
            });
    }
}
