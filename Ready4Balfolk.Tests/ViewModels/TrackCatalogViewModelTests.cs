using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using NSubstitute;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.TrackCatalog;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>The library as a sortable grid on a desk.</summary>
public sealed class TrackCatalogViewModelTests : IDisposable
{
    private readonly SourceList<Track> _tracks = new();
    private readonly BehaviorSubject<bool> _isLoading = new(false);
    private readonly BehaviorSubject<int> _inReviewCount = new(0);
    private readonly BehaviorSubject<ApplicationSettings> _settings =
        new(new ApplicationSettings() with { MusicDirectoryPath = "/music" });
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ILibraryIndex _libraryIndex = Substitute.For<ILibraryIndex>();
    private readonly TrackCatalogViewModel _sut;

    public TrackCatalogViewModelTests()
    {
        var trackStore = Substitute.For<ITrackStore>();
        trackStore.IsLoading.Returns(_isLoading);
        trackStore.InReviewCount.Returns(_inReviewCount);
        // The catalog asks for a filtered connection; the search term is threaded through it.
        trackStore.Connect(Arg.Any<IObservable<string>>())
            .Returns(call => _tracks.Connect().Filter(
                call.Arg<IObservable<string>>().Select(TrackSearchFilter.For)));

        _queueService.Enqueue(Arg.Any<IQueueItem>()).Returns(QueueAddResult.Allow());

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_ => _settings.Value);
        settingsStore.Observe().Returns(_settings);

        _sut = new TrackCatalogViewModel(
            trackStore, _queueService, _notifications, new TrackEditorService(
                Substitute.For<IDanceListStore>(), _libraryIndex, trackStore), settingsStore);
    }

    private static async Task SettleAsync() => await Task.Delay(450);

    [Fact]
    public async Task Tracks_AreSortedByDance()
    {
        // The grid opens sorted, so a DJ scanning for "the mazurkas" finds them together.
        _tracks.AddRange([
            TestData.CreateTrack(dance: "Scottish"),
            TestData.CreateTrack(dance: "Bourree"),
            TestData.CreateTrack(dance: "Mazurka")
        ]);

        await SettleAsync();

        Assert.Equal(["Bourree", "Mazurka", "Scottish"], _sut.Tracks.Select(t => t.Dance));
    }

    [Fact]
    public async Task SearchText_NarrowsTheGrid()
    {
        _tracks.AddRange([
            TestData.CreateTrack(dance: "Mazurka", artist: "Naragonia"),
            TestData.CreateTrack(dance: "Scottish", artist: "Someone")
        ]);
        await SettleAsync();

        _sut.SearchText = "naragonia";
        await SettleAsync();

        var only = Assert.Single(_sut.Tracks);
        Assert.Equal("Naragonia", only.Artist);
    }

    [Fact]
    public async Task ClearSearch_PutsEverythingBack()
    {
        _tracks.AddRange([
            TestData.CreateTrack(dance: "Mazurka", artist: "Naragonia"),
            TestData.CreateTrack(dance: "Scottish", artist: "Someone")
        ]);
        _sut.SearchText = "naragonia";
        await SettleAsync();

        _sut.ClearSearchCommand.Execute().Subscribe();
        await SettleAsync();
        await SettleAsync();

        Assert.Equal(string.Empty, _sut.SearchText);
        Assert.Equal(2, _sut.Tracks.Count);
    }

    [Fact]
    public async Task EnqueueTrack_TheQueueRefuses_SaysSoWhereTheUserIsLooking()
    {
        // The queue guard's own reason, as a notification. A silent refusal in front of a room is
        // the DJ pressing the button again and wondering.
        _queueService.Enqueue(Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("The queue would run past the cutoff"));
        _tracks.Add(TestData.CreateTrack());
        await SettleAsync();

        _sut.EnqueueTrackCommand.Execute(_sut.Tracks[0]).Subscribe();
        await SettleAsync();

        _notifications.Received(1).Show(
            "The queue would run past the cutoff", NotificationSeverity.Warning);
    }

    [Fact]
    public async Task EnqueueTrack_Accepted_SaysNothing()
    {
        _tracks.Add(TestData.CreateTrack());
        await SettleAsync();

        _sut.EnqueueTrackCommand.Execute(_sut.Tracks[0]).Subscribe();
        await SettleAsync();

        _queueService.Received(1).Enqueue(Arg.Any<TrackQueueItem>());
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
    }

    [Fact]
    public async Task TakingAnAnswerBack_TakesItBackAndSaysWhereTheTrackWent()
    {
        // An answer given a week ago, long after the row that gave it was rebuilt out of the review
        // queue. The catalogue is where a track that got through the gate still exists, so this is
        // where taking its answer back has to live: nowhere else is there anything to press.
        _libraryIndex.WithdrawIndividualApprovalsAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(3);
        _tracks.Add(TestData.CreateTrack(title: "Salamandre"));
        await SettleAsync();
        var track = _sut.Tracks[0];

        await _sut.WithdrawTrackCommand.Execute(track);

        await _libraryIndex.Received(1).WithdrawIndividualApprovalsAsync(
            Arg.Is<IReadOnlyCollection<string>>(paths => paths.Contains(track.Track.FileInfo.FullName)),
            Arg.Any<CancellationToken>());
        _notifications.Received(1).Show(
            Arg.Is<string>(said => said.Contains("Salamandre", StringComparison.Ordinal)),
            NotificationSeverity.Information);
    }

    [Fact]
    public async Task TakingBackAnAnswerNobodyGave_SaysSoRatherThanDoingNothingQuietly()
    {
        // The track is in the library on a rule or on its own tags. A menu entry that quietly does
        // nothing reads as one that failed, and the DJ presses it again.
        _libraryIndex.WithdrawIndividualApprovalsAsync(
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(0);
        _tracks.Add(TestData.CreateTrack(title: "Salamandre"));
        await SettleAsync();

        await _sut.WithdrawTrackCommand.Execute(_sut.Tracks[0]);

        _notifications.Received(1).Show(
            Arg.Is<string>(said => said.Contains("Salamandre", StringComparison.Ordinal)),
            NotificationSeverity.Warning);
    }

    [Fact]
    public void IsLoading_FollowsTheStore()
    {
        _isLoading.OnNext(true);

        Assert.True(_sut.IsLoading);
    }

    [Fact]
    public async Task EmptyGrid_WithTracksWaitingInReview_SaysHowMany()
    {
        _inReviewCount.OnNext(5);
        await SettleAsync();

        Assert.Contains("5", _sut.EmptyStateText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyGrid_WithNoMusicFolderSet_SaysSoRatherThanBlamingReview()
    {
        _settings.OnNext(_settings.Value with { MusicDirectoryPath = "" });
        _inReviewCount.OnNext(5);
        await SettleAsync();

        Assert.Equal(UiStrings.TrackCatalog_EmptyNoFolder, _sut.EmptyStateText);
    }

    [Fact]
    public async Task EmptyGrid_FolderSetAndNothingWaiting_SaysTheLibraryIsEmpty()
    {
        await SettleAsync();

        Assert.Equal(UiStrings.TrackCatalog_EmptyNoTracks, _sut.EmptyStateText);
    }

    [Fact]
    public async Task SearchWithNoMatches_SaysNothingMatchedRatherThanTheLibraryIsEmpty()
    {
        // A search that comes up empty is not the same situation as an empty library, even though
        // both leave the grid blank: the DJ typed something and wants to know it did not match.
        _inReviewCount.OnNext(5);
        _tracks.Add(TestData.CreateTrack(dance: "Mazurka", artist: "Naragonia"));
        await SettleAsync();

        _sut.SearchText = "nothing matches this";
        await SettleAsync();

        Assert.Equal(UiStrings.TrackCatalog_EmptySearch, _sut.EmptyStateText);
    }

    [Fact]
    public async Task SearchOverAnAlreadyEmptyGrid_ExplainsTheSearchAndKeepsUpWithIt()
    {
        // The first-run case: nothing in the library yet because the gate is holding it all in
        // review. Searching there moves no row, since the grid was empty before the keystroke and
        // is empty after it, so nothing the grid publishes says the situation changed at all.
        _inReviewCount.OnNext(5);
        await SettleAsync();

        _sut.SearchText = "matches nothing";
        await SettleAsync();
        Assert.Equal(UiStrings.TrackCatalog_EmptySearch, _sut.EmptyStateText);

        // And a second term that also matches nothing is still the DJ's search being answered,
        // not the message from before it left standing.
        _sut.SearchText = "matches nothing either";
        await SettleAsync();
        Assert.Equal(UiStrings.TrackCatalog_EmptySearch, _sut.EmptyStateText);

        // And clearing it hands the explaining back to review, with the grid still holding no row
        // that could have moved to say so.
        _sut.SearchText = "";
        await SettleAsync();
        Assert.Contains("5", _sut.EmptyStateText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonEmptyGrid_HasNoEmptyStateText()
    {
        _inReviewCount.OnNext(5);
        _tracks.Add(TestData.CreateTrack());
        await SettleAsync();

        Assert.Equal(string.Empty, _sut.EmptyStateText);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _tracks.Dispose();
        _isLoading.Dispose();
        _inReviewCount.Dispose();
        _settings.Dispose();
    }
}
