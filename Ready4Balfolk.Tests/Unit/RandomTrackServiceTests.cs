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
    public void Pool_NeverPicksADanceCarryingAnExcludedTag()
    {
        // "bretagne, but never suite": plinn carries both, so the exclusion wins and nothing is
        // eligible.
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.Pool(["bretagne"], ["suite"]), true));
    }

    [Fact]
    public void Pool_ExclusionsAloneNarrowTheWholeList()
    {
        // Nothing chosen, one thing forbidden: everything except the common dances.
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.Pool([], ["common"]), true);

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

    [Fact]
    public void TracksAllAtTheUsualLikelihood_SplitTheirDanceEvenly()
    {
        // Exactly as a pick always behaved: a third each for three recordings of one dance.
        var weights = RandomTrackService.Weigh(
            ["mazurka"],
            [TestData.CreateTrack(title: "A"), TestData.CreateTrack(title: "B"), TestData.CreateTrack(title: "C")]);

        Assert.All(weights, candidate => Assert.Equal(1.0 / 3, candidate.Weight, 12));
    }

    [Fact]
    public void ATrackAtFour_IsFourTimesAsLikelyAsOneAtOneOfTheSameDance()
    {
        var favourite = TestData.CreateTrack(title: "Favourite") with { Likelihood = 4 };
        var usual = TestData.CreateTrack(title: "Usual");

        var weights = RandomTrackService.Weigh(["mazurka"], [favourite, usual]);

        Assert.Equal(4, WeightOf(weights, favourite) / WeightOf(weights, usual), 12);
    }

    [Fact]
    public void ATrackAtAQuarter_IsAQuarterAsLikelyAsOneAtOneOfTheSameDance()
    {
        var rare = TestData.CreateTrack(title: "Rare") with { Likelihood = 0.25 };
        var usual = TestData.CreateTrack(title: "Usual");

        var weights = RandomTrackService.Weigh(["mazurka"], [rare, usual]);

        Assert.Equal(0.25, WeightOf(weights, rare) / WeightOf(weights, usual), 12);
    }

    [Fact]
    public void ALikelihood_NeverMovesTheDancesShareOfThePool()
    {
        // The multiplier works inside the dance and nowhere else: a waltz whose one favourite is at
        // x4 comes up exactly as often as a plinn nobody touched, and each dance holds one share.
        var weights = RandomTrackService.Weigh(
            ["mazurka", "plinn"],
            [
                TestData.CreateTrack(title: "Favourite") with { Likelihood = 4 },
                TestData.CreateTrack(title: "Usual"),
                TestData.CreateTrack(title: "Rare") with { Likelihood = 0.25 },
                TestData.CreateTrack("Plinn", title: "Only")
            ]);

        var shareByDance = weights
            .GroupBy(candidate => candidate.Track.DanceSlug)
            .ToDictionary(group => group.Key!, group => group.Sum(candidate => candidate.Weight));

        Assert.Equal(1, shareByDance["mazurka"], 12);
        Assert.Equal(1, shareByDance["plinn"], 12);
    }

    [Fact]
    public void AFavouredTrack_ComesUpMoreOftenThanTheRestOfItsDance()
    {
        // The pick itself, not only its weights: over many draws the x4 track has to win most of
        // them. Loose bounds, because this is chance; 4 to 1 is 80 percent and the bound is 65.
        var favourite = TestData.CreateTrack(title: "Favourite") with { Likelihood = 4 };
        Tracks(favourite, TestData.CreateTrack(title: "Usual"));

        var favouriteCount = Enumerable.Range(0, 1000)
            .Count(_ => _sut.PickRandomTrack(new RandomSelectionScope.SingleDance("mazurka"), true) == favourite);

        Assert.InRange(favouriteCount, 650, 950);
    }

    private static double WeightOf(List<(Track Track, double Weight)> weights, Track track) =>
        weights.Single(candidate => candidate.Track == track).Weight;

    private void Tracks(params Track[] tracks) => _trackStore.Current.Returns(tracks.ToList());
}
