using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using RxUnit = System.Reactive.Unit;

namespace Ready4Balfolk.Tests.Integration;

public sealed class QueueConsumptionServiceTests : IDisposable
{
    private readonly IAudioPlaybackService _audio;
    private readonly QueueService _queue;
    private readonly IQueueHistoryStore _history;
    private readonly QueueConsumptionService _sut;

    private readonly Subject<RxUnit> _playbackStarted = new();
    private readonly Subject<RxUnit> _playbackPaused = new();
    private readonly Subject<RxUnit> _playbackRestarted = new();
    private readonly Subject<RxUnit> _playbackCleared = new();
    private readonly Subject<RxUnit> _playbackEnded = new();
    private readonly Subject<TimeSpan> _progressChanged = new();
    private readonly Subject<TimeSpan> _durationChanged = new();

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

        var settingsStore = Substitute.For<ISettingsStore>();
        var settings = new ApplicationSettings() with
        {
            MaxQueueItems = 100
        };
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        _queue = new QueueService(settingsStore, _history, () => null, () => TimeSpan.Zero, new NoOpLoggerService());

        _sut = new QueueConsumptionService(_audio, _queue, _history, new NoOpLoggerService());
    }

    [Fact]
    public async Task AdvanceAsync_DequeuesAndStartsTrack()
    {
        var mockFileSystem = new MockFileSystem();

        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
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
        var mockFileSystem = new MockFileSystem();

        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
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
        var mockFileSystem = new MockFileSystem();

        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
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
        var mockFileSystem = new MockFileSystem();

        IQueueItem? lastItem = null;
        using var sub = _sut.WhenCurrentItemChanged.Subscribe(item => lastItem = item);

        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
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
        var mockFileSystem = new MockFileSystem();

        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        _queue.Enqueue(auto);

        await _sut.AdvanceAsync();

        Assert.Equal(auto, _sut.CurrentItem);
        await _audio.Received(1).SelectAsync(Arg.Any<Uri>());
        await _audio.Received(1).PlayAsync();
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
