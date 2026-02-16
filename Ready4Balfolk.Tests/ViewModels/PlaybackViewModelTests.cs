using System.Reactive.Subjects;
using DynamicData;
using NSubstitute;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Playback;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class PlaybackViewModelTests : IDisposable
{
    private readonly PlaybackViewModel _sut;

    private readonly BehaviorSubject<IQueueItem?> _currentItem = new(null);
    private readonly BehaviorSubject<TimeSpan> _elapsed = new(TimeSpan.Zero);
    private readonly BehaviorSubject<TimeSpan> _totalDuration = new(TimeSpan.Zero);
    private readonly BehaviorSubject<bool> _isPlaying = new(false);
    private readonly SourceList<IQueueItem> _queueSource = new();

    public PlaybackViewModelTests()
    {
        var consumption = Substitute.For<IQueueConsumptionService>();
        consumption.WhenCurrentItemChanged.Returns(_currentItem);
        consumption.WhenElapsedChanged.Returns(_elapsed);
        consumption.WhenTotalDurationChanged.Returns(_totalDuration);
        consumption.WhenIsPlayingChanged.Returns(_isPlaying);

        var queue = Substitute.For<IQueueService>();
        queue.Connect().Returns(_queueSource.Connect());
        queue.Count.Returns(_ => _queueSource.Count);

        var confirmation = Substitute.For<IConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new ApplicationSettings());

        _sut = new PlaybackViewModel(consumption, queue, confirmation, settingsStore, Substitute.For<IAudioPlaybackService>());
    }

    // --- Current item display ---

    [Fact]
    public void TrackItem_SetsTrackDisplay()
    {
        var track = new TrackQueueItem(TestData.CreateTrack("Mazurka", "Artist1", "Title1"), false);
        _currentItem.OnNext(track);

        Assert.Equal("Mazurka", _sut.DanceName);
        Assert.Equal("Artist1", _sut.ArtistName);
        Assert.Equal("Title1", _sut.TrackTitle);
        Assert.True(_sut.HasTrack);
        Assert.False(_sut.IsMessageMode);
        Assert.True(_sut.HasCurrentItem);
    }

    [Fact]
    public void AutoTrackItem_SetsTrackDisplay()
    {
        var auto = new AutoTrackQueueItem(new TrackQueueItem(
            TestData.CreateTrack("Waltz", "ArtistW", "TitleW"), true));
        _currentItem.OnNext(auto);

        Assert.Equal("Waltz", _sut.DanceName);
        Assert.Equal("ArtistW", _sut.ArtistName);
        Assert.Equal("TitleW", _sut.TrackTitle);
        Assert.True(_sut.HasTrack);
    }

    [Fact]
    public void MessageItem_SetsMessageMode()
    {
        var message = new MessageQueueItem("Hello World");
        _currentItem.OnNext(message);

        Assert.True(_sut.IsMessageMode);
        Assert.False(_sut.HasTrack);
        Assert.Equal("Hello World", _sut.DanceName);
    }

    [Fact]
    public void DelayItem_SetsDelayDisplay()
    {
        var delay = new DelayQueueItem(TimeSpan.FromSeconds(30));
        _currentItem.OnNext(delay);

        Assert.False(_sut.HasTrack);
        Assert.Equal("Delay", _sut.DanceName);
    }

    [Fact]
    public void StopItem_SetsStopDisplay()
    {
        var stop = new StopQueueItem();
        _currentItem.OnNext(stop);

        Assert.False(_sut.HasTrack);
        Assert.Equal("Stop", _sut.DanceName);
    }

    [Fact]
    public void NullItem_ClearsState()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack(), false));
        _currentItem.OnNext(null);

        Assert.False(_sut.HasTrack);
        Assert.False(_sut.IsMessageMode);
        Assert.Equal("", _sut.DanceName);
        Assert.False(_sut.HasCurrentItem);
    }

    // --- IsPlaying ---

    [Fact]
    public void IsPlaying_ReflectsService()
    {
        _isPlaying.OnNext(true);
        Assert.True(_sut.IsPlaying);

        _isPlaying.OnNext(false);
        Assert.False(_sut.IsPlaying);
    }

    // --- Progress/Duration ---

    [Fact]
    public void Progress_UpdatesFromElapsed()
    {
        _elapsed.OnNext(TimeSpan.FromSeconds(42));
        Assert.Equal(42, _sut.Progress);
        Assert.Equal("0:42", _sut.CurrentTime);
    }

    [Fact]
    public void Duration_UpdatesFromTotal()
    {
        _totalDuration.OnNext(TimeSpan.FromMinutes(3));
        Assert.Equal(180, _sut.Duration);
        Assert.Equal("3:00", _sut.TotalTime);
    }

    // --- ShowNextIcon ---

    [Fact]
    public void ShowNextIcon_TrueWhenQueueHasItems()
    {
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));
        Assert.True(_sut.ShowNextIcon);
    }

    [Fact]
    public void ShowNextIcon_FalseWhenQueueEmpty() => Assert.False(_sut.ShowNextIcon);

    public void Dispose()
    {
        _sut.Dispose();
        _currentItem.Dispose();
        _elapsed.Dispose();
        _totalDuration.Dispose();
        _isPlaying.Dispose();
        _queueSource.Dispose();
    }
}
