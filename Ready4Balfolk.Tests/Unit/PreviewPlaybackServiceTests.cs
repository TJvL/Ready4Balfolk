using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class PreviewPlaybackServiceTests : IDisposable
{
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly IQueueConsumptionService _consumption = Substitute.For<IQueueConsumptionService>();
    private readonly Subject<IQueueItem?> _currentItems = new();
    private readonly Subject<System.Reactive.Unit> _ended = new();
    private IQueueItem? _currentItem;
    private readonly PreviewPlaybackService _sut;

    // Absolute on both platforms: PlayAsync builds a Uri from it, and "/music/a.mp3" is a valid
    // file URI on Linux and a relative path on Windows, where the constructor throws.
    private static readonly string TrackPath = Path.Combine(Path.GetTempPath(), "a.mp3");

    public PreviewPlaybackServiceTests()
    {
        _playback.WhenPlaybackEnded.Returns(_ended);
        _consumption.CurrentItem.Returns(_ => _currentItem);
        _consumption.WhenCurrentItemChanged.Returns(_currentItems);

        _sut = new PreviewPlaybackService(_playback, _consumption);
    }

    [Fact]
    public async Task APreview_PlaysTheFileAndSaysWhichItIs()
    {
        Assert.True(await _sut.PlayAsync(TrackPath));

        await _playback.Received(1).SelectAsync(new Uri(TrackPath));
        await _playback.Received(1).PlayAsync();
        Assert.Equal(TrackPath, _sut.Previewing);
    }

    [Fact]
    public async Task APreview_WhileTheQueueOwnsTheOutput_IsRefused()
    {
        // The room is dancing to that output. A preview on it is the hall hearing a file nobody
        // queued.
        _currentItem = new TrackQueueItem(TestData.CreateTrack(), false);

        Assert.False(await _sut.PlayAsync(TrackPath));

        await _playback.DidNotReceive().SelectAsync(Arg.Any<Uri>());
        Assert.Null(_sut.Previewing);
    }

    [Fact]
    public async Task APreviewThatRunsOut_ClosesItself()
    {
        await _sut.PlayAsync(TrackPath);

        _ended.OnNext(System.Reactive.Unit.Default);

        Assert.Null(_sut.Previewing);
    }

    [Fact]
    public async Task StoppingWithNothingPreviewed_LeavesTheOutputAlone()
    {
        await _sut.StopAsync();

        await _playback.DidNotReceive().ClearAsync();
    }

    [Fact]
    public async Task Seeking_MovesThePreviewAndNothingElse()
    {
        // Nothing previewed: the output may be the queue's, and seeking it would jump the dance.
        await _sut.SeekAsync(TimeSpan.FromSeconds(30));
        await _playback.DidNotReceive().SeekAsync(Arg.Any<TimeSpan>());

        await _sut.PlayAsync(TrackPath);
        await _sut.SeekAsync(TimeSpan.FromSeconds(30));

        await _playback.Received(1).SeekAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task TheQueueTakingTheOutput_EndsThePreviewWithoutTouchingPlayback()
    {
        await _sut.PlayAsync(TrackPath);

        // The room starts dancing: the queue owns the one output now.
        _currentItem = new TrackQueueItem(TestData.CreateTrack(), false);
        _currentItems.OnNext(_currentItem);

        Assert.Null(_sut.Previewing);
        await _playback.DidNotReceive().ClearAsync();
    }

    [Fact]
    public async Task StoppingAStalePreview_NeverSilencesTheQueue()
    {
        await _sut.PlayAsync(TrackPath);
        _currentItem = new TrackQueueItem(TestData.CreateTrack(), false);

        // Even if the takeover signal was missed, stopping must not clear the queue's stream.
        await _sut.StopAsync();

        await _playback.DidNotReceive().ClearAsync();
    }

    [Fact]
    public async Task StoppingAPreview_ClearsTheOutputWhenNobodyElseOwnsIt()
    {
        await _sut.PlayAsync(TrackPath);

        await _sut.StopAsync();

        Assert.Null(_sut.Previewing);
        await _playback.Received(1).ClearAsync();
    }

    [Fact]
    public async Task Disposing_LeavesTheSubjectUsable_SoALateEndOfTrackCannotTakeTheProcessDown()
    {
        await _sut.PlayAsync(TrackPath);

        _sut.Dispose();

        // The end of a preview is reported from BASS's own thread, and can already be on its way
        // when disposal returns. A disposed subject threw there, where nothing can catch it.
        Assert.Null(Record.Exception(() => _sut.WhenPreviewChanged.Subscribe(_ => { }).Dispose()));
        Assert.Null(Record.Exception(() => _sut.Previewing));
    }

    public void Dispose()
    {
        _sut.Dispose();
        _currentItems.Dispose();
        _ended.Dispose();
    }
}
