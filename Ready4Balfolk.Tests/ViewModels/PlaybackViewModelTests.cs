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

    public PlaybackViewModelTests()
    {
        _consumption = Substitute.For<IQueueConsumptionService>();
        _consumption.WhenCurrentItemChanged.Returns(_currentItem);
        _consumption.WhenElapsedChanged.Returns(_elapsed);
        _consumption.WhenTotalDurationChanged.Returns(_totalDuration);
        _consumption.WhenIsPlayingChanged.Returns(_isPlaying);
        _consumption.CurrentItem.Returns(_ => _currentItem.Value);
        _consumption.AdvanceAsync(Arg.Any<IQueueItem?>()).Returns(true);

        var queue = Substitute.For<IQueueService>();
        queue.Connect().Returns(_queueSource.Connect());
        queue.Count.Returns(_ => _queueSource.Count);

        _confirmation = Substitute.For<IConfirmationService>();
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>()).Returns(true);

        var settingsStore = Substitute.For<ISettingsStore>();
        var settings = new ApplicationSettings();
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        _sut = new PlaybackViewModel(_consumption, queue, _confirmation, _notifications, settingsStore, Substitute.For<IAudioPlaybackService>());
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
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                given = call.Arg<CancellationToken>();
                _currentItem.OnNext(next);
                return false;
            });

        await _sut.RestartCommand.Execute();

        Assert.True(given.IsCancellationRequested);
    }

    [Fact]
    public async Task AQuestionAboutTheFloor_AsksWithTheKeyboardOnTheSafeAnswer()
    {
        _currentItem.OnNext(new TrackQueueItem(TestData.CreateTrack("Mazurka"), false));

        await _sut.RestartCommand.Execute();

        await _confirmation.Received(1).ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            ConfirmationStakes.Destructive, Arg.Any<CancellationToken>());
    }

    /// <summary>Says yes to the next question, after letting the evening move on underneath it.</summary>
    private void AnswerYesAfter(Action whileTheQuestionIsUp) =>
        _confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
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
        _queueSource.Dispose();
    }
}
