using System.Reactive.Linq;
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
    private IQueueItem? _currentItem;
    private readonly PreviewPlaybackService _sut;

    // Absolute on both platforms: PlayAsync builds a Uri from it, and "/music/a.mp3" is a valid
    // file URI on Linux and a relative path on Windows, where the constructor throws.
    private static readonly string TrackPath = Path.Combine(Path.GetTempPath(), "a.mp3");

    public PreviewPlaybackServiceTests()
    {
        _playback.WhenPlaybackEnded.Returns(Observable.Never<System.Reactive.Unit>());
        _consumption.CurrentItem.Returns(_ => _currentItem);
        _consumption.WhenCurrentItemChanged.Returns(_currentItems);

        _sut = new PreviewPlaybackService(_playback, _consumption);
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

    public void Dispose()
    {
        _sut.Dispose();
        _currentItems.Dispose();
    }
}
