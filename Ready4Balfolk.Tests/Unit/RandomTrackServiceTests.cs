using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class RandomTrackServiceTests
{
    private readonly IDanceListStore _danceListStore = Substitute.For<IDanceListStore>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly IQueueHistoryStore _historyStore = Substitute.For<IQueueHistoryStore>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly IQueueConsumptionService _consumptionService = Substitute.For<IQueueConsumptionService>();
    private readonly RandomTrackService _sut;

    public RandomTrackServiceTests()
    {
        _sut = new RandomTrackService(_danceListStore, _trackStore, _historyStore, _queueService, _consumptionService);
        _danceListStore.Current.Returns(TestData.CreateSimpleDanceList());
        _historyStore.Current.Returns(new QueueHistory(null, []));
        _queueService.Items.Returns(new List<IQueueItem>());
        _consumptionService.CurrentItem.Returns((IQueueItem?)null);
    }

    [Fact]
    public void EntireList_ReturnsAMatchingTrack()
    {
        Tracks(TestData.CreateTrack());

        var result = _sut.PickRandomTrack(RandomSelectionScope.EntireList, true);

        Assert.NotNull(result);
        Assert.Equal("mazurka", result.DanceSlug);
    }

    [Fact]
    public void Pool_PicksOnlyFromDancesCarryingATagInIt()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.Pool(["bretagne"]), true);

        Assert.NotNull(result);
        Assert.Equal("plinn", result.DanceSlug);
    }

    [Fact]
    public void Pool_IsAUnion_NotAnIntersection()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        // Two tags nothing carries together still reach both dances, because a pool is what to
        // draw from rather than a filter to satisfy.
        var slugs = new HashSet<string?>();
        for (var i = 0; i < 60; i++)
        {
            slugs.Add(_sut.PickRandomTrack(new RandomSelectionScope.Pool(["bretagne", "common"]), true)?.DanceSlug);
        }

        Assert.Contains("plinn", slugs);
        Assert.Contains("mazurka", slugs);
    }

    [Fact]
    public void EmptyPool_ReachesEverything()
    {
        Tracks(TestData.CreateTrack("Plinn"));

        Assert.NotNull(_sut.PickRandomTrack(new RandomSelectionScope.Pool([]), true));
    }

    [Fact]
    public void PoolNothingCarries_ReturnsNull()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.Pool(["sweden"]), true));
    }

    [Fact]
    public void SingleDance_PicksThatDanceOnly()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.SingleDance("plinn"), true);

        Assert.NotNull(result);
        Assert.Equal("plinn", result.DanceSlug);
    }

    [Fact]
    public void SingleDance_UnknownSlug_ReturnsNull()
    {
        Tracks(TestData.CreateTrack());

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.SingleDance("nope"), true));
    }

    [Fact]
    public void TrackTheListDoesNotKnow_IsNeverPicked()
    {
        // An unresolved track has no dance to be weighted by, so it cannot take part.
        Tracks(TestData.CreateTrack("An Tri dipop", slug: null));

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, true));
    }

    [Fact]
    public void DanceWithNoTracks_IsSkipped()
    {
        Tracks(TestData.CreateTrack("Plinn"));

        var result = _sut.PickRandomTrack(RandomSelectionScope.EntireList, true);

        Assert.NotNull(result);
        Assert.Equal("plinn", result.DanceSlug);
    }

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        _danceListStore.Current.Returns(DanceList.Empty);
        Tracks(TestData.CreateTrack());

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, true));
    }

    [Fact]
    public void NoTracks_ReturnsNull()
    {
        Tracks();

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, true));
    }

    [Fact]
    public void EveryDanceInThePool_CanComeUp()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        var slugs = new HashSet<string?>();
        for (var i = 0; i < 60; i++)
        {
            slugs.Add(_sut.PickRandomTrack(RandomSelectionScope.EntireList, true)?.DanceSlug);
        }

        // No weights any more: what is in the pool is equally likely, however the list is shaped.
        Assert.Equal(2, slugs.Count);
    }

    [Fact]
    public void AlreadyFinishedTrack_IsExcludedWhenDuplicatesAreNotAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _historyStore.Current.Returns(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, track.Dance, track.Artist, track.Title,
                track.Length, false, CompletionStatus.Finished)
        ]));

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, false));
    }

    [Fact]
    public void QueuedTrack_IsExcludedWhenDuplicatesAreNotAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _queueService.Items.Returns(new List<IQueueItem> { new TrackQueueItem(track, false) });

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, false));
    }

    [Fact]
    public void PlayingTrack_IsExcludedWhenDuplicatesAreNotAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _consumptionService.CurrentItem.Returns(new TrackQueueItem(track, false));

        Assert.Null(_sut.PickRandomTrack(RandomSelectionScope.EntireList, false));
    }

    [Fact]
    public void ExcludedTrack_IsStillPickedWhenDuplicatesAreAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _consumptionService.CurrentItem.Returns(new TrackQueueItem(track, false));

        Assert.NotNull(_sut.PickRandomTrack(RandomSelectionScope.EntireList, true));
    }

    private void Tracks(params Track[] tracks) => _trackStore.Current.Returns(tracks.ToList());
}
