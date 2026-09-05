using System.Reactive.Linq;
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
    private readonly IQueueConsumptionService _consumption;
    private readonly IConfirmationService _confirmation;
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    private readonly BehaviorSubject<IQueueItem?> _currentItem = new(null);
    private readonly BehaviorSubject<TimeSpan> _elapsed = new(TimeSpan.Zero);
    private readonly BehaviorSubject<TimeSpan> _totalDuration = new(TimeSpan.Zero);
    private readonly BehaviorSubject<bool> _isPlaying = new(false);
    private readonly SourceList<IQueueItem> _queueSource = new();

    /// <summary>An output that is there, which is what the buttons being offered at all depends on.</summary>
    private readonly BehaviorSubject<bool> _audioAvailable = new(true);

    public PlaybackViewModelTests()
    {
        _consumption = Substitute.For<IQueueConsumptionService>();
        _consumption.WhenCurrentItemChanged.Returns(_currentItem);
        _consumption.WhenElapsedChanged.Returns(_elapsed);
        _consumption.WhenTotalDurationChanged.Returns(_totalDuration);
        _consumption.WhenIsPlayingChanged.Returns(_isPlaying);
        _consumption.CurrentItem.Returns(_ => _currentItem.Value);
        _consumption.AdvanceAsync(Arg.Any<IQueueItem?>()).Returns(true);
        _consumption.PlayPauseAsync().Returns(true);
        _consumption.RestartAsync().Returns(true);
        _consumption.SeekAsync(Arg.Any<TimeSpan>()).Returns(true);

        var queue = Substitute.For<IQueueService>();
        queue.Connect().Returns(_queueSource.Connect());
        queue.Count.Returns(_ => _queueSource.Count);

        _confirmation = Substitute.For<IConfirmationService>();
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var settingsStore = Substitute.For<ISettingsStore>();
        var settings = new ApplicationSettings();
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        var audio = Substitute.For<IAudioPlaybackService>();
        audio.WhenAvailabilityChanged.Returns(_audioAvailable);

        _sut = new PlaybackViewModel(_consumption, queue, _confirmation, _notifications, settingsStore, audio);
    }

    // --- Current item display ---

    [Fact]
    public void TrackItem_SetsTrackDisplay()
    {
        var track = new TrackQueueItem(TestData.CreateTrack("Mazurka", "Artist1", "Title1"), false);
        _currentItem.OnNext(track);

        Assert.Equal("Mazurka", _sut.DanceName);
        Assert.Equal("Artist1 - Title1", _sut.TrackLine);
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
        Assert.Equal("ArtistW - TitleW", _sut.TrackLine);
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

    // --- What the transport buttons are offered for ---

    /// <summary>
    /// There is no stream behind the moment between two dances, so there is nothing to hold or to
    /// start again: the buttons that act on one are not offered, and next is what moves the evening.
    /// </summary>
    [Fact]
    public void DuringAGap_HoldingAndStartingAgainAreNotOffered()
    {
        var canPlayPause = true;
        var canRestart = true;
        using var playPause = _sut.PlayPauseCommand.CanExecute.Subscribe(value => canPlayPause = value);
        using var restart = _sut.RestartCommand.CanExecute.Subscribe(value => canRestart = value);

        _currentItem.OnNext(new GapQueueItem(TimeSpan.FromSeconds(10)));

        Assert.False(canPlayPause);
        Assert.False(canRestart);
    }

    /// <summary>Nothing has started yet, so play is what starts it.</summary>
    [Fact]
    public void WithNothingOnAndSomethingWaiting_PlayIsOffered()
    {
        var canPlayPause = false;
        var canRestart = true;
        using var playPause = _sut.PlayPauseCommand.CanExecute.Subscribe(value => canPlayPause = value);
        using var restart = _sut.RestartCommand.CanExecute.Subscribe(value => canRestart = value);

        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack(), false));

        Assert.True(canPlayPause);
        Assert.False(canRestart);
    }

    [Fact]
    public void WithNothingOnAndNothingWaiting_NeitherIsOffered()
    {
        var canPlayPause = true;
        using var playPause = _sut.PlayPauseCommand.CanExecute.Subscribe(value => canPlayPause = value);

        Assert.False(canPlayPause);
    }

    /// <summary>The closing music is a file playing, so it is held and started again like a dance.</summary>
    [Fact]
    public void OnTheEndOfTheNight_BothAreOffered()
    {
        var canPlayPause = false;
        var canRestart = false;
        using var playPause = _sut.PlayPauseCommand.CanExecute.Subscribe(value => canPlayPause = value);
        using var restart = _sut.RestartCommand.CanExecute.Subscribe(value => canRestart = value);

        _currentItem.OnNext(new EndOfNightQueueItem(EndOfNightPath, TimeSpan.FromMinutes(4)));

        Assert.True(canPlayPause);
        Assert.True(canRestart);
    }

    [Fact]
    public async Task PlayPause_RefusedByTheService_SaysSo()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));
        _consumption.PlayPauseAsync().Returns(false);

        await _sut.PlayPauseCommand.Execute();

        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Restart_RefusedByTheService_SaysSo()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));
        _consumption.RestartAsync().Returns(false);

        await _sut.RestartCommand.Execute();

        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    // The path this platform's Uri accepts, which is what the closing music is queued with.
    private static readonly string EndOfNightPath = Path.GetFullPath("/audio/last-waltz.mp3");

    // --- A confirmation answered after the dance it was about ended ---

    [Fact]
    public async Task Skip_ConfirmedAfterTheDanceEnded_LeavesTheNewOneAlone()
    {
        var playing = new TrackQueueItem(TestData.CreateTrack("Mazurka"), false);
        _currentItem.OnNext(playing);
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("Waltz"), false));

        var next = new TrackQueueItem(TestData.CreateTrack("Waltz"), false);
        AnswerYesAfter(() => _currentItem.OnNext(next));

        await _sut.NextOrClearCommand.Execute();

        await _consumption.DidNotReceive().AdvanceAsync(Arg.Any<IQueueItem?>());
        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Skip_AnsweredInTime_AdvancesTheItemItWasAbout()
    {
        var playing = new TrackQueueItem(TestData.CreateTrack("Mazurka"), false);
        _currentItem.OnNext(playing);
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("Waltz"), false));

        await _sut.NextOrClearCommand.Execute();

        await _consumption.Received(1).AdvanceAsync(playing);
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
    }

    [Fact]
    public async Task Skip_RefusedByTheService_SaysSo()
    {
        var playing = new TrackQueueItem(TestData.CreateTrack("Mazurka"), false);
        _currentItem.OnNext(playing);
        _queueSource.Add(new TrackQueueItem(TestData.CreateTrack("Waltz"), false));
        _consumption.AdvanceAsync(Arg.Any<IQueueItem?>()).Returns(false);

        await _sut.NextOrClearCommand.Execute();

        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Restart_ConfirmedAfterTheDanceEnded_DoesNotRestartTheNewOne()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));

        var next = new TrackQueueItem(TestData.CreateTrack("Waltz"), false);
        AnswerYesAfter(() => _currentItem.OnNext(next));

        await _sut.RestartCommand.Execute();

        await _consumption.DidNotReceive().RestartAsync();
        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Seek_ConfirmedAfterTheDanceEnded_DoesNotSeekTheNewOne()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));

        var next = new TrackQueueItem(TestData.CreateTrack("Waltz"), false);
        AnswerYesAfter(() => _currentItem.OnNext(next));

        await _sut.SeekCommand.Execute(TimeSpan.FromSeconds(90));

        await _consumption.DidNotReceive().SeekAsync(Arg.Any<TimeSpan>());
        _notifications.Received(1).Show(Arg.Any<string>(), NotificationSeverity.Warning);
    }

    [Fact]
    public async Task AQuestionOnScreen_IsWithdrawnWhenTheDanceEnds()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));

        CancellationToken given = default;
        var next = new TrackQueueItem(TestData.CreateTrack("Waltz"), false);
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                given = call.Arg<CancellationToken>();
                _currentItem.OnNext(next);
                return false;
            });

        await _sut.RestartCommand.Execute();

        Assert.True(given.IsCancellationRequested);
    }

    /// <summary>Says yes to the next question, after letting the evening move on underneath it.</summary>
    private void AnswerYesAfter(Action whileTheQuestionIsUp) =>
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                whileTheQuestionIsUp();
                return true;
            });

    public void Dispose()
    {
        _sut.Dispose();
        _currentItem.Dispose();
        _elapsed.Dispose();
        _totalDuration.Dispose();
        _isPlaying.Dispose();
        _audioAvailable.Dispose();
        _queueSource.Dispose();
    }
}
