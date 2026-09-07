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
    /// <summary>What each of the two lines is doing, so it can be called off and restarted.</summary>
    private IDisposable? _trackInfoScroll;
    private IDisposable? _messageScroll;

    /// <summary>Whether the press that is in flight started on the bar, so a drag from elsewhere
    /// does not land as a seek when the button comes up over it.</summary>
    private bool _pressedOnTheBar;

    public PlaybackView()
    {
        InitializeComponent();

        PlaybackProgressBar.PointerPressed += OnProgressBarPointerPressed;
        PlaybackProgressBar.PointerReleased += OnProgressBarPointerReleased;
        PlaybackProgressBar.PointerCaptureLost += (_, _) => _pressedOnTheBar = false;

        WhenEitherIsResized(TrackInfoCanvas, TrackInfoPanel)
            .Subscribe(_ => _trackInfoScroll = Slide(_trackInfoScroll, TrackInfoCanvas, TrackInfoPanel));

        // The same treatment for the message line. It needs it more than the track line does: a
        // track's artist and title are as long as a library makes them, and a message is as long as
        // the DJ typed it.
        WhenEitherIsResized(MessageCanvas, MessageTextBlock)
            .Subscribe(_ => _messageScroll = Slide(_messageScroll, MessageCanvas, MessageTextBlock));
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

        if (!wasPressed || ViewModel is not { HasAudioItem: true, Duration: > 0 } vm)
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

    /// <summary>
    /// Fires whenever a line or the room it has to sit in is laid out afresh. Bounds carry where a
    /// control sits as well as how big it is, so a move counts as much as a resize.
    /// </summary>
    private static IObservable<(Rect Room, Rect Line)> WhenEitherIsResized(Canvas room, Control line) =>
        room.GetObservable(BoundsProperty)
            .CombineLatest(line.GetObservable(BoundsProperty))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>
    /// Centres a line in the room it has, or slides it back and forth when it is wider than that.
    /// </summary>
    /// <returns>
    /// What is sliding it, to be disposed when the line is measured again, or nothing when it fits
    /// and nothing needs to move.
    /// </returns>
    private static IDisposable? Slide(IDisposable? sliding, Canvas room, Control line)
    {
        sliding?.Dispose();

        var roomWidth = room.Bounds.Width;
        var lineWidth = line.Bounds.Width;

        if (roomWidth <= 0 || lineWidth <= 0)
        {
            return null;
        }

        if (lineWidth <= roomWidth)
        {
            Canvas.SetLeft(line, (roomWidth - lineWidth) / 2);
            return null;
        }

        // Overflows: scroll back and forth
        var overflow = lineWidth - roomWidth;
        const double scrollSpeed = 50.0; // px/sec
        const double pauseSec = 2.0;
        var scrollSec = overflow / scrollSpeed;
        var cycleSec = pauseSec + scrollSec + pauseSec + scrollSec;

        var startTime = DateTimeOffset.UtcNow;

        return Observable.Interval(TimeSpan.FromMilliseconds(16))
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

                Canvas.SetLeft(line, left);
            });
    }
}
