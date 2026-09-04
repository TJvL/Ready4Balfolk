using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;

namespace Ready4Balfolk.Domain.Services.Presentation;

/// <inheritdoc cref="IPresentationStateService" />
public sealed class PresentationStateService : IPresentationStateService, IDisposable
{
    private readonly IQueueService _queueService;
    private readonly CompositeDisposable _disposables = [];
    private readonly BehaviorSubject<PresentationState> _state = new(PresentationState.Empty);
    private readonly BehaviorSubject<PresentationProgress> _progress = new(PresentationProgress.Zero);
    private readonly Lock _gate = new();

    private IQueueItem? _currentItem;
    private bool _isPlaying;
    private TimeSpan _elapsed;
    private TimeSpan _duration;

    public PresentationState Current => _state.Value;

    public IObservable<PresentationState> WhenStateChanged => _state.DistinctUntilChanged();

    public IObservable<PresentationProgress> WhenProgressChanged => _progress.AsObservable();

    public PresentationStateService(IQueueConsumptionService consumptionService, IQueueService queueService)
    {
        ArgumentNullException.ThrowIfNull(consumptionService);
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));

        _disposables.Add(consumptionService.WhenCurrentItemChanged
            .Subscribe(item =>
            {
                lock (_gate)
                {
                    _currentItem = item;
                }

                PublishState();
            }));

        _disposables.Add(consumptionService.WhenIsPlayingChanged
            .Subscribe(playing =>
            {
                lock (_gate)
                {
                    _isPlaying = playing;
                }

                PublishState();
            }));

        // The next item lives in the queue rather than in the player, so any queue change can
        // change what the bottom half of the display shows.
        _disposables.Add(queueService.Connect().Subscribe(_ => PublishState()));

        _disposables.Add(consumptionService.WhenElapsedChanged
            .Subscribe(elapsed =>
            {
                lock (_gate)
                {
                    _elapsed = elapsed;
                }

                PublishProgress();
            }));

        _disposables.Add(consumptionService.WhenTotalDurationChanged
            .Subscribe(duration =>
            {
                lock (_gate)
                {
                    _duration = duration;
                }

                PublishProgress();
            }));
    }

    private void PublishState()
    {
        PresentationState next;
        lock (_gate)
        {
            // Both read from one snapshot of the queue rather than a peek and then a second look,
            // so what is next and what is behind it can never come from different moments.
            var queued = _queueService.Items;

            next = new PresentationState(
                Map(_currentItem),
                Map(queued.Count > 0 ? queued[0] : null),
                Behind(queued),
                _isPlaying);
        }

        _state.OnNext(next);
    }

    /// <summary>
    /// The dance waiting behind a pause, and nothing in any other case.
    /// </summary>
    /// <remarks>
    /// Only a delay, a stop or a message is stood behind. Those are the items a room is given to
    /// get ready during, and the question they raise is which dance they are getting ready for.
    /// Anything else that is next answers that question by itself.
    /// </remarks>
    private static PresentationItem Behind(IReadOnlyList<IQueueItem> queued)
    {
        if (queued.Count < 2)
        {
            return PresentationItem.None;
        }

        var next = Map(queued[0]);
        if (next.Kind is not (PresentationItemKind.Delay or PresentationItemKind.Stop
            or PresentationItemKind.Message))
        {
            return PresentationItem.None;
        }

        var behind = Map(queued[1]);
        return behind.Kind is PresentationItemKind.Track ? behind : PresentationItem.None;
    }

    private void PublishProgress()
    {
        PresentationProgress next;
        lock (_gate)
        {
            next = new PresentationProgress(_elapsed, _duration);
        }

        _progress.OnNext(next);
    }

    /// <summary>The one place a queue item becomes something a screen can draw.</summary>
    public static PresentationItem Map(IQueueItem? item) => item switch
    {
        null => PresentationItem.None,
        AutoTrackQueueItem auto => MapTrack(auto.TrackQueueItem),
        TrackQueueItem track => MapTrack(track),
        // The message text is the large line; there is no artist or title beneath it.
        MessageQueueItem message => new PresentationItem(
            PresentationItemKind.Message, message.Description, string.Empty, string.Empty),
        // Delay and stop carry no payload at all: each surface writes its own label, so the desktop
        // window keeps reading UiStrings and the browser keeps its own.
        DelayQueueItem => new PresentationItem(
            PresentationItemKind.Delay, string.Empty, string.Empty, string.Empty),
        StopQueueItem => new PresentationItem(
            PresentationItemKind.Stop, string.Empty, string.Empty, string.Empty),
        EndOfNightQueueItem => new PresentationItem(
            PresentationItemKind.EndOfNight, string.Empty, string.Empty, string.Empty),
        _ => PresentationItem.None
    };

    private static PresentationItem MapTrack(TrackQueueItem trackItem) => new(
        PresentationItemKind.Track,
        trackItem.Track.Dance,
        trackItem.Track.Artist,
        trackItem.Track.Title);

    public void Dispose()
    {
        _disposables.Dispose();
        _state.Dispose();
        _progress.Dispose();
    }
}
