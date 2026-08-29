using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using DynamicData;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The dance panel: the published list, browsed, and the pool a random pick draws from.
/// </summary>
/// <remarks>
/// Nothing here edits the list, so what the panel is for is choosing. The pool narrows what is
/// shown as well as what is drawn, which is the part worth holding: a panel that claims to draw
/// from something the user cannot see is a panel they cannot check.
/// </remarks>
public sealed class DanceListViewModelTests : IDisposable
{
    private static readonly DanceList List = new()
    {
        Tags = ["bretagne", "common", "suite"],
        Dances =
        [
            TestData.CreateDance("mazurka", ["common"], "Mazurka", "Mazurk"),
            TestData.CreateDance("scottish", ["common"], "Scottish", "Schottische"),
            TestData.CreateDance("plinn", ["bretagne", "suite"], "Plinn")
        ]
    };

    private readonly BehaviorSubject<DanceList> _lists = new(List);
    private readonly BehaviorSubject<DanceListStatus> _status = new(DanceListStatus.Unknown);
    private readonly BehaviorSubject<DancePoolSelection> _pools = new(DancePoolSelection.Everything);
    private readonly SourceList<Track> _tracks = new();
    private readonly IDanceListStore _store = Substitute.For<IDanceListStore>();
    private readonly IDancePool _pool = Substitute.For<IDancePool>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly IRandomTrackService _randomTracks = Substitute.For<IRandomTrackService>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IDanceListFeed _feed = Substitute.For<IDanceListFeed>();
    private readonly DanceListViewModel _sut;

    public DanceListViewModelTests()
    {
        _store.Observe().Returns(_lists);
        _store.ObserveStatus().Returns(_status);
        _store.Current.Returns(_ => _lists.Value);
        _store.Index.Returns(_ => DanceListIndex.Build(_lists.Value));
        _store.IsLoading.Returns(Observable.Return(false));
        _pool.Observe().Returns(_pools);
        _trackStore.Connect().Returns(_tracks.Connect());
        _trackStore.Current.Returns(_ => _tracks.Items.ToList());
        _feed.HomePage.Returns(new Uri("https://tjvl.github.io/BigBalfolkList/"));
        _queueService.Enqueue(Arg.Any<IQueueItem>()).Returns(QueueAddResult.Allow());

        _sut = new DanceListViewModel(_store, _pool, _trackStore, _randomTracks, _queueService,
            _notifications, _feed, new MockFileSystem(), new NoOpLoggerService());
    }

    private static async Task SettleAsync() => await Task.Delay(400);

    private DanceCardViewModel Card(string slug) => _sut.Dances.Single(card => card.Slug == slug);

    private TagChipViewModel Chip(string tag) => _sut.Tags.Single(chip => chip.Tag == tag);

    // --- What the panel shows ---

    [Fact]
    public void TheCards_AreInAlphabeticalOrder() =>
        // The panel is browsed by eye rather than searched, so the order is the whole navigation.
        Assert.Equal(["Mazurka · Mazurk", "Plinn", "Scottish · Schottische"],
            _sut.Dances.Select(card => card.NamesText));

    [Fact]
    public async Task ACard_CountsTheTracksYouHaveForThatDance()
    {
        _tracks.AddRange([
            TestData.CreateTrack(dance: "Mazurka", title: "One"),
            TestData.CreateTrack(dance: "Mazurka", title: "Two"),
            TestData.CreateTrack(dance: "Plinn")
        ]);
        await SettleAsync();

        Assert.Equal(2, Card("mazurka").TrackCount);
        Assert.True(Card("plinn").HasTracks);
        Assert.False(Card("scottish").HasTracks);
    }

    [Fact]
    public void TheSummary_SaysHowManyOfHowMany() =>
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Summary, 3, 3),
            _sut.SummaryText);

    [Fact]
    public void TheSourceLink_IsTheReadablePageRatherThanTheRawFile() =>
        Assert.DoesNotContain("raw.githubusercontent", _sut.SourceUri.ToString(), StringComparison.Ordinal);

    // --- Searching ---

    [Fact]
    public async Task Search_NarrowsTheCards()
    {
        _sut.SearchText = "plinn";
        await SettleAsync();

        Assert.Equal("plinn", Assert.Single(_sut.Dances).Slug);
    }

    [Fact]
    public async Task Search_MatchesAnySpellingOfADance()
    {
        // Every name is an equal: somebody typing the German spelling is not searching wrong.
        _sut.SearchText = "schottische";
        await SettleAsync();

        Assert.Equal("scottish", Assert.Single(_sut.Dances).Slug);
    }

    [Fact]
    public async Task Search_IgnoresAccentsAndCase()
    {
        _sut.SearchText = "MAZURK";
        await SettleAsync();

        Assert.Equal("mazurka", Assert.Single(_sut.Dances).Slug);
    }

    [Fact]
    public async Task Search_ATagThatNothingShowingCarries_IsDimmedRatherThanRemoved()
    {
        // A rail that reshuffles itself as you type is impossible to aim at.
        _sut.SearchText = "plinn";
        await SettleAsync();

        Assert.True(Chip("common").IsDimmed);
        Assert.False(Chip("bretagne").IsDimmed);
        Assert.Equal(3, _sut.Tags.Count);
    }

    // --- The pool ---

    [Fact]
    public void ThePool_StartsAsEverything()
    {
        Assert.False(_sut.HasPool);
        Assert.Equal(UiStrings.DanceList_PoolEverything, _sut.PoolDescription);
    }

    [Fact]
    public void APool_NarrowsWhatIsShownAndNotOnlyWhatIsDrawn()
    {
        _pools.OnNext(new DancePoolSelection(["bretagne"], []));

        Assert.Equal("plinn", Assert.Single(_sut.Dances).Slug);
        Assert.True(_sut.HasPool);
        Assert.True(Chip("bretagne").IsInPool);
    }

    [Fact]
    public void AnExclusion_BeatsAnInclusion()
    {
        // Plinn is both bretagne and suite. Drawing from bretagne but never from suite has to
        // leave it out, here as in the draw itself.
        _pools.OnNext(new DancePoolSelection(["bretagne"], ["suite"]));

        Assert.Empty(_sut.Dances);
        Assert.True(Chip("suite").IsExcluded);
    }

    [Fact]
    public void ThePoolDescription_NamesTheTagsAndCountsWhatIsDrawable()
    {
        _pools.OnNext(new DancePoolSelection(["common"], []));

        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_PoolFormat, "common", 2),
            _sut.PoolDescription);
    }

    [Fact]
    public void ThePoolDescription_SaysWhatIsNeverDrawn()
    {
        _pools.OnNext(new DancePoolSelection([], ["bretagne"]));

        Assert.Contains(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_PoolNever, "bretagne"),
            _sut.PoolDescription,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ToggleTag_IsThePoolsDecisionRatherThanThePanels()
    {
        _sut.ToggleTagCommand.Execute("common").Subscribe();

        _pool.Received(1).Toggle("common");
    }

    [Fact]
    public void ClearPool_GoesBackToEverything()
    {
        _sut.ClearPoolCommand.Execute().Subscribe();

        _pool.Received(1).Clear();
    }

    // --- The dice on a card ---

    [Fact]
    public void PickDance_QueuesARandomTrackOfThatDanceAlone()
    {
        // Whatever the pool happens to be: the dice on a card is asking for that one dance.
        var track = TestData.CreateTrack();
        _randomTracks.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>()).Returns(track);

        _sut.PickDanceCommand.Execute("mazurka").Subscribe();

        _randomTracks.Received(1).PickRandomTrack(
            Arg.Is<RandomSelectionScope.SingleDance>(scope => scope.Slug == "mazurka"), false);
        _queueService.Received(1).Enqueue(Arg.Is<TrackQueueItem>(item => item.RandomlyAdded));
    }

    [Fact]
    public void PickDance_NothingToPlay_SaysSoByName()
    {
        // "You have no track for Mazurka" and not for "mazurka": the slug is an identifier and the
        // user never chose it.
        _randomTracks.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>()).Returns((Track?)null);

        _sut.PickDanceCommand.Execute("mazurka").Subscribe();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_NoTrackForDance, "Mazurka"),
            NotificationSeverity.Warning);
        _queueService.DidNotReceive().Enqueue(Arg.Any<IQueueItem>());
    }

    [Fact]
    public void PickDance_TheQueueRefuses_ShowsTheQueuesOwnReason()
    {
        _randomTracks.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>())
            .Returns(TestData.CreateTrack());
        _queueService.Enqueue(Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("The evening has been declared over"));

        _sut.PickDanceCommand.Execute("mazurka").Subscribe();

        _notifications.Received(1).Show("The evening has been declared over", NotificationSeverity.Warning);
    }

    // --- Updating the list ---

    [Fact]
    public async Task Update_Updated_SaysHowManyAreNew()
    {
        _store.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Updated(4));

        await _sut.UpdateCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Updated, 4),
            NotificationSeverity.Information);
    }

    [Fact]
    public async Task Update_AlreadyCurrent_SaysSoWithoutFuss()
    {
        _store.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Unchanged);

        await _sut.UpdateCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(UiStrings.DanceList_AlreadyCurrent, NotificationSeverity.Information);
    }

    [Fact]
    public async Task Update_Failed_IsAWarningRatherThanAnError()
    {
        // A hall with no wifi is the normal case. The list already in hand carries on working, so
        // this is not the application breaking.
        _store.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Failed("no route to host"));

        await _sut.UpdateCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, "no route to host"),
            NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Update_WhileItRuns_TheButtonSaysSo()
    {
        var fetching = new TaskCompletionSource<DanceListUpdate>();
        _store.RefreshAsync(Arg.Any<CancellationToken>()).Returns(fetching.Task);

        var running = _sut.UpdateCommand.Execute().FirstAsync().ToTask();
        Assert.True(_sut.IsUpdating);

        fetching.SetResult(DanceListUpdate.Unchanged);
        await running;

        Assert.False(_sut.IsUpdating);
    }

    [Fact]
    public async Task UpdateFromFile_TheFileIsUnreadable_IsReportedRatherThanThrown()
    {
        // The path came from a file picker, so anything can be behind it, including a directory.
        _store.UpdateFromFileAsync(Arg.Any<IFileInfo>(), Arg.Any<CancellationToken>())
            .Returns<Task<DanceListUpdate>>(_ => throw new IOException("that is a folder"));

        await _sut.UpdateFromFileAsync("/somewhere/dances.json");

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, "that is a folder"),
            NotificationSeverity.Error);
        Assert.False(_sut.IsUpdating);
    }

    // --- Where the list came from ---

    [Fact]
    public void Origin_ShippedWithTheApplication_SaysSo() =>
        Assert.Equal(UiStrings.DanceList_ObtainedBuiltIn, _sut.OriginText);

    [Fact]
    public void Origin_Downloaded_SaysWhen()
    {
        // A stale list has to be visible rather than assumed: it is the vocabulary everything else
        // in the application is said in.
        var obtained = new DateTimeOffset(2026, 8, 20, 19, 30, 0, TimeSpan.Zero);
        _status.OnNext(new DanceListStatus(3, 3, DanceListOrigin.Downloaded, obtained));

        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Obtained, obtained.ToLocalTime().DateTime),
            _sut.OriginText);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _lists.Dispose();
        _status.Dispose();
        _pools.Dispose();
        _tracks.Dispose();
    }
}
