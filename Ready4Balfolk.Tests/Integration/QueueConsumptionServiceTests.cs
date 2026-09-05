using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using RxUnit = System.Reactive.Unit;

namespace Ready4Balfolk.Tests.Integration;

public sealed class QueueConsumptionServiceTests : IDisposable
{
    private readonly IAudioPlaybackService _audio;
    private readonly QueueService _queue;
    private readonly IQueueHistoryStore _history;
    private readonly ISettingsStore _settingsStore;
    private readonly QueueConsumptionService _sut;

    private readonly Subject<RxUnit> _playbackStarted = new();
    private readonly Subject<RxUnit> _playbackPaused = new();
    private readonly Subject<RxUnit> _playbackRestarted = new();
    private readonly Subject<RxUnit> _playbackCleared = new();
    private readonly Subject<RxUnit> _playbackEnded = new();
    private readonly Subject<TimeSpan> _progressChanged = new();
    private readonly Subject<TimeSpan> _durationChanged = new();

    /// <summary>Read live, so a test can change a setting after the service is built.</summary>
    private ApplicationSettings _settings = new();

    public QueueConsumptionServiceTests()
    {
        _audio = Substitute.For<IAudioPlaybackService>();
        _audio.WhenPlaybackStarted.Returns(_playbackStarted);
        _audio.WhenPlaybackPaused.Returns(_playbackPaused);
        _audio.WhenPlaybackRestarted.Returns(_playbackRestarted);
        _audio.WhenPlaybackCleared.Returns(_playbackCleared);
        _audio.WhenPlaybackEnded.Returns(_playbackEnded);
        _audio.WhenProgressChanged.Returns(_progressChanged);
        _audio.WhenDurationChanged.Returns(_durationChanged);

        _history = Substitute.For<IQueueHistoryStore>();
        _history.Current.Returns(new QueueHistory(null, []));

        _settingsStore = Substitute.For<ISettingsStore>();
        _settings = new ApplicationSettings() with
        {
            MaxQueueItems = 100
        };
        _settingsStore.Current.Returns(_ => _settings);
        _settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(_settings));

        var trackStore = Substitute.For<ITrackStore>();
        trackStore.WhenTrackFileVanished.Returns(Observable.Never<string>());

        _queue = new QueueService(
            _settingsStore, _history, trackStore, () => null, () => TimeSpan.Zero, new NoOpLoggerService(),
            TimeProvider.System);

        _sut = new QueueConsumptionService(
            _audio, _queue, _history, _settingsStore, new NoOpLoggerService(), TimeProvider.System,
            ImmediateScheduler.Instance);
    }

    [Fact]
    public async Task AdvanceAsync_DequeuesAndStartsTrack()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _queue.Enqueue(track);

        await _sut.AdvanceAsync();

        Assert.Equal(track, _sut.CurrentItem);
        await _audio.Received(1).SelectAsync(Arg.Any<Uri>());
        await _audio.Received(1).PlayAsync();
    }

    [Fact]
    public async Task AdvanceAsync_EmptyQueue_ClearsCurrentItem()
    {
        await _sut.AdvanceAsync();

        Assert.Null(_sut.CurrentItem);
    }

    [Fact]
    public async Task AdvanceAsync_RecordsHistoryEntry()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _queue.Enqueue(track);
        await _sut.AdvanceAsync();

        // Now advance again (which records the first track as skipped)
        await _sut.AdvanceAsync();

        await _history.Received(1)
            .AddAsync(Arg.Is<TrackHistoryEntry>(e => e!.CompletionStatus == CompletionStatus.Skipped));
    }

    [Fact]
    public async Task AdvanceAsync_RecordsWhenTheTrackStarted()
    {
        var before = DateTime.Now;
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _queue.Enqueue(track);
        await _sut.AdvanceAsync();
        await _sut.AdvanceAsync();

        await _history.Received(1).AddAsync(Arg.Is<TrackHistoryEntry>(e =>
            e!.StartedAt != null && e.StartedAt >= before && e.StartedAt <= DateTime.Now));
    }

    [Fact]
    public async Task PlayPauseAsync_DelegatesToAudio()
    {
        _audio.IsPlaying.Returns(true);
        await _sut.PlayPauseAsync();
        await _audio.Received(1).PauseAsync();

        _audio.IsPlaying.Returns(false);
        await _sut.PlayPauseAsync();
        await _audio.Received(1).PlayAsync();
    }

    [Fact]
    public async Task RestartAsync_DelegatesToAudio()
    {
        await _sut.RestartAsync();
        await _audio.Received(1).RestartAsync();
    }

    [Fact]
    public async Task ClearAsync_ClearsCurrentItemAndAudio()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _queue.Enqueue(track);
        await _sut.AdvanceAsync();

        await _sut.ClearAsync();

        Assert.Null(_sut.CurrentItem);
        await _audio.Received().ClearAsync();
    }

    [Fact]
    public void CurrentItem_InitiallyNull() => Assert.Null(_sut.CurrentItem);

    [Fact]
    public async Task WhenCurrentItemChanged_Emits()
    {
        IQueueItem? lastItem = null;
        using var sub = _sut.WhenCurrentItemChanged.Subscribe(item => lastItem = item);

        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        _queue.Enqueue(track);
        await _sut.AdvanceAsync();

        Assert.Equal(track, lastItem);
    }

    [Fact]
    public void WhenIsPlayingChanged_EmitsOnAudioEvents()
    {
        var isPlaying = false;
        using var sub = _sut.WhenIsPlayingChanged.Subscribe(v => isPlaying = v);

        _playbackStarted.OnNext(RxUnit.Default);
        Assert.True(isPlaying);

        _playbackPaused.OnNext(RxUnit.Default);
        Assert.False(isPlaying);
    }

    [Fact]
    public async Task DelayItem_ClearsAudioAndSetsCurrentItem()
    {
        var delay = new DelayQueueItem(TimeSpan.FromSeconds(5));
        _queue.Enqueue(delay);

        await _sut.AdvanceAsync();

        Assert.Equal(delay, _sut.CurrentItem);
        await _audio.Received().ClearAsync();
    }

    [Fact]
    public async Task MessageItem_SetsCurrentItem()
    {
        var message = new MessageQueueItem("Hello");
        _queue.Enqueue(message);

        await _sut.AdvanceAsync();

        Assert.Equal(message, _sut.CurrentItem);
    }

    [Fact]
    public async Task StopItem_ClearsAudio()
    {
        var stop = new StopQueueItem();
        _queue.Enqueue(stop);

        await _sut.AdvanceAsync();

        Assert.Equal(stop, _sut.CurrentItem);
        await _audio.Received().ClearAsync();
    }

    [Fact]
    public async Task AutoTrackItem_PlaysUnderlyingTrack()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        _queue.Enqueue(auto);

        await _sut.AdvanceAsync();

        Assert.Equal(auto, _sut.CurrentItem);
        await _audio.Received(1).SelectAsync(Arg.Any<Uri>());
        await _audio.Received(1).PlayAsync();
    }

    // Absolute in this platform's own notation: what EndOfNightAudio resolves the setting to, and
    // the only shape System.Uri accepts on Windows.
    private static readonly string EndOfNightPath = Path.GetFullPath("/audio/last-waltz.mp3");

    [Fact]
    public async Task AdvanceAsync_WithAGapAsked_WaitsBeforeTheNextDance()
    {
        _settings = _settings with { GapBetweenTracksEnabled = true, GapBetweenTracksSeconds = 5 };
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));

        await _sut.AdvanceAsync();
        _playbackEnded.OnNext(RxUnit.Default);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // The floor's moment: the gap is what is playing, so every screen can draw it, and the
        // coming dance is still in the queue rather than taken out of it.
        Assert.IsType<GapQueueItem>(_sut.CurrentItem);
        Assert.Equal(1, _queue.Count);

        // And it is never written down: a night's account is what was played and what was decided.
        await _history.DidNotReceive().AddAsync(Arg.Any<DelayHistoryEntry>());
    }

    [Fact]
    public async Task AdvanceAsync_WithNoGapAsked_StartsTheNextDanceAtOnce()
    {
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));

        await _sut.AdvanceAsync();
        _playbackEnded.OnNext(RxUnit.Default);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.NotNull(_sut.CurrentItem);
        Assert.Equal(0, _queue.Count);
    }

    [Fact]
    public async Task AdvanceAsync_EndOfNight_PlaysTheChosenFile()
    {
        var endOfNight = new EndOfNightQueueItem(EndOfNightPath, TimeSpan.FromMinutes(4));
        _queue.Enqueue(endOfNight);

        await _sut.AdvanceAsync();

        Assert.Equal(endOfNight, _sut.CurrentItem);
        await _audio.Received(1).SelectAsync(new Uri(EndOfNightPath));
        await _audio.Received(1).PlayAsync();
    }

    [Fact]
    public async Task AdvanceAsync_EndOfNight_RecordsThatTheEveningEnded()
    {
        _queue.Enqueue(new EndOfNightQueueItem(EndOfNightPath, TimeSpan.FromMinutes(4)));
        await _sut.AdvanceAsync();

        await _sut.AdvanceAsync();

        await _history.Received(1).AddAsync(Arg.Is<EndOfNightHistoryEntry>(e =>
            e!.Duration == TimeSpan.FromMinutes(4) && e.StartedAt != null));
    }

    /// <summary>A file that was not there is recorded as that, not as somebody skipping it.</summary>
    [Fact]
    public async Task AdvanceAsync_AFileThatWillNotOpen_IsRecordedAsMissingRatherThanSkipped()
    {
        _audio.SelectAsync(Arg.Any<Uri>()).Returns(_ => throw new InvalidOperationException("gone"));
        _queue.Enqueue(new EndOfNightQueueItem(EndOfNightPath, TimeSpan.FromMinutes(4)));

        await _sut.AdvanceAsync();

        await _history.Received(1).AddAsync(Arg.Is<QueueHistoryEntry>(e =>
            e!.CompletionStatus == CompletionStatus.FileMissing));
    }

    /// <summary>Nothing can follow the end of the night, so the night is filed and the next opens.</summary>
    [Fact]
    public async Task AdvanceAsync_EndOfNight_FilesTheNight()
    {
        _queue.Enqueue(new EndOfNightQueueItem(EndOfNightPath, TimeSpan.FromMinutes(4)));
        await _sut.AdvanceAsync();

        await _sut.AdvanceAsync();

        // Filed at the moment the closing song finished rather than a beat later.
        await _history.Received(1).EndNightAsync(Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task AdvanceAsync_Track_DoesNotFileTheNight()
    {
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        await _sut.AdvanceAsync();

        await _sut.AdvanceAsync();

        await _history.DidNotReceive().EndNightAsync(Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Advance_WhenTheNextItemWillNotPreload_SaysSoRatherThanLosingIt()
    {
        using var logger = new RecordingLoggerService();
        using var sut = CreateServiceLoggingTo(logger);

        // What BASS raises for the track after this one: the file is there and will not open.
        _audio.PreloadNextAsync(Arg.Any<Uri>())
            .Returns(Task.FromException(new InvalidOperationException("Failed to create preload stream")));

        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));

        await sut.AdvanceAsync();

        // Preloading is nobody's await, so this used to be a bare discard: the exception sat on an
        // unobserved task until a garbage collection, and the DJ heard about it when the room did.
        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DomainStrings.Queue_PreloadFailed, reported.Message);
        Assert.IsType<InvalidOperationException>(reported.Exception);
    }

    [Fact]
    public async Task WhenATrackEndsAndTheAdvanceFails_SaysSoRatherThanLosingIt()
    {
        using var logger = new RecordingLoggerService();
        using var sut = CreateServiceLoggingTo(logger);

        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));
        await sut.AdvanceAsync();

        // A locked history file, which is what filing the finished track runs into.
        _history.AddAsync(Arg.Any<QueueHistoryEntry>())
            .Returns(Task.FromException(new InvalidOperationException("history.sqlite is locked")));

        _playbackEnded.OnNext(RxUnit.Default);

        var reported = await logger.NextErrorAsync(TestContext.Current.CancellationToken);
        Assert.Equal(DomainStrings.Queue_AdvanceFailed, reported.Message);
        Assert.IsType<InvalidOperationException>(reported.Exception);
    }

    [Fact]
    public async Task WhenTheApplicationClosesAsATrackEnds_TheCloseIsNotReportedAsAFailure()
    {
        using var logger = new RecordingLoggerService();
        var sut = CreateServiceLoggingTo(logger);

        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));
        await sut.AdvanceAsync();

        // The window closing in the moment the track ends. The end-of-track callback has already
        // started, and the gate it is about to wait on is gone by the time it gets there: an
        // ordinary close, which used to read as an ERROR line and fail the smoke test's log scan.
        var closing = false;
        using var closeWhileTheCallbackRuns = sut.WhenIsPlayingChanged.Subscribe(_ =>
        {
            if (closing)
            {
                sut.Dispose();
            }
        });
        closing = true;

        _playbackEnded.OnNext(RxUnit.Default);

        Assert.Empty(logger.Errors);
        sut.Dispose();
    }

    /// <summary>A scheduler that keeps what it is handed until a test lets it go.</summary>
    /// <remarks>
    /// It stands for the UI thread being busy: work put on it is waiting, which is what makes
    /// "the queue was not touched yet" something a test can assert rather than race against.
    /// </remarks>
    private sealed class HeldScheduler : IScheduler
    {
        private readonly List<Action> _waiting = [];

        public DateTimeOffset Now => DateTimeOffset.UtcNow;

        public int Waiting => _waiting.Count;

        public void RunAll()
        {
            var due = _waiting.ToArray();
            _waiting.Clear();
            foreach (var work in due)
            {
                work();
            }
        }

        public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        {
            _waiting.Add(() => action(this, state));
            return Disposable.Empty;
        }

        public IDisposable Schedule<TState>(
            TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action) =>
            Schedule(state, action);

        public IDisposable Schedule<TState>(
            TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action) =>
            Schedule(state, action);
    }

    [Fact]
    public async Task ATrackEnding_DoesNotDequeueOnTheThreadTheCallbackArrivedOn()
    {
        // The end-of-track callback comes from the audio library's own thread and the countdown
        // from a timer thread, while everything else that touches the queue runs on the UI thread.
        // A dequeue from either of those is what moves the queue under a removal already in flight.
        var held = new HeldScheduler();
        using var sut = new QueueConsumptionService(
            _audio, _queue, _history, _settingsStore, new NoOpLoggerService(), TimeProvider.System,
            held);

        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(title: "Second"), false));
        await sut.AdvanceAsync();

        _playbackEnded.OnNext(RxUnit.Default);

        Assert.Equal(1, _queue.Count);
        Assert.Equal(1, held.Waiting);

        held.RunAll();
        Assert.Equal(0, _queue.Count);
    }

    /// <summary>A second service on a clock a test can move, sharing this fixture's doubles.</summary>
    private QueueConsumptionService CreateServiceOn(TimeProvider time) =>
        new(_audio, _queue, _history, _settingsStore, new NoOpLoggerService(), time,
            ImmediateScheduler.Instance);

    /// <summary>A second service that keeps what it reports, sharing this fixture's doubles.</summary>
    private QueueConsumptionService CreateServiceLoggingTo(ILoggerService logger) =>
        new(_audio, _queue, _history, _settingsStore, logger, TimeProvider.System,
            ImmediateScheduler.Instance);

    [Fact]
    public async Task ACountdownThatHasExpired_StopsTicking_WhileTheHistoryWriteIsStillGoing()
    {
        var time = new FakeTimeProvider();
        using var sut = CreateServiceOn(time);

        var elapsed = new List<TimeSpan>();
        using var watchingElapsed = sut.WhenElapsedChanged.Subscribe(elapsed.Add);

        var delay = TimeSpan.FromSeconds(5);
        _queue.Enqueue(new DelayQueueItem(delay));
        _queue.Enqueue(new TrackQueueItem(TestData.CreateTrack(), false));

        await sut.AdvanceAsync();

        // Holding the write open is the whole point of the test: cleaning the item up is what used
        // to stop the countdown, and that only happens once the history row is down.
        var writing = new TaskCompletionSource();
        _history.AddAsync(Arg.Any<QueueHistoryEntry>()).Returns(_ => writing.Task);

        time.Advance(delay);
        time.Advance(TimeSpan.FromSeconds(10));

        // One expiry, one advance. Every tick that landed while the write was in flight used to
        // queue another, and the second of them filed the dance that had just started as skipped.
        Assert.Equal(1, elapsed.Count(value => value == delay));

        var advanced = new TaskCompletionSource();
        using var watchingItem = sut.WhenCurrentItemChanged
            .Where(item => item is TrackQueueItem)
            .Take(1)
            .Subscribe(_ => advanced.TrySetResult());

        writing.SetResult();
        await advanced.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Disposing_LeavesTheSubjectsUsable_SoALateTickCannotTakeTheProcessDown()
    {
        var sut = CreateServiceOn(TimeProvider.System);

        sut.Dispose();

        // A countdown tick or an end-of-track callback from the audio thread can already be on its
        // way when disposal returns, because stopping a timer does not wait for a callback that has
        // started. Against a disposed subject that threw ObjectDisposedException on a threadpool
        // thread, which no handler in the application can catch and which ends the process.
        Assert.Null(Record.Exception(() => sut.WhenElapsedChanged.Subscribe(_ => { }).Dispose()));
        Assert.Null(Record.Exception(() => sut.WhenIsPlayingChanged.Subscribe(_ => { }).Dispose()));
        Assert.Null(Record.Exception(() => sut.WhenCurrentItemChanged.Subscribe(_ => { }).Dispose()));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _queue.Dispose();
        _playbackStarted.Dispose();
        _playbackPaused.Dispose();
        _playbackRestarted.Dispose();
        _playbackCleared.Dispose();
        _playbackEnded.Dispose();
        _progressChanged.Dispose();
        _durationChanged.Dispose();
    }
}
