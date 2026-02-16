using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ready4Balfolk.UI.Views.Playback;

public partial class PlaybackView : ReactiveUserControl<PlaybackViewModel>
{
    private IDisposable? _trackInfoScroll;

    public PlaybackView()
    {
        InitializeComponent();

        TrackInfoCanvas.GetObservable(BoundsProperty)
            .CombineLatest(TrackInfoPanel.GetObservable(BoundsProperty))
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateTrackInfoScroll());
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

        // Overflows — scroll back and forth
        var overflow = panelWidth - canvasWidth;
        const double scrollSpeed = 50.0; // px/sec
        const double pauseSec = 2.0;
        var scrollSec = overflow / scrollSpeed;
        var cycleSec = pauseSec + scrollSec + pauseSec + scrollSec;

        var startTime = DateTimeOffset.UtcNow;

        _trackInfoScroll = Observable.Interval(TimeSpan.FromMilliseconds(16))
            .ObserveOn(RxApp.MainThreadScheduler)
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
