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

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true);

        Assert.NotNull(result);
        Assert.Equal("mazurka", result.DanceSlug);
    }

    [Fact]
    public void Category_PicksOnlyFromInsideIt()
    {
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        // Bretagne is the second root category and holds only the plinn.
        var result = _sut.PickRandomTrack(new RandomSelectionScope.Category([1]), true);

        Assert.NotNull(result);
        Assert.Equal("plinn", result.DanceSlug);
    }

    [Fact]
    public void Category_ReachesDancesInItsSubCategories()
    {
        Tracks(TestData.CreateTrack("Plinn"));

        // The plinn sits one level down, in "Suite plinn".
        var result = _sut.PickRandomTrack(new RandomSelectionScope.Category([1]), true);

        Assert.NotNull(result);
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
    public void CategoryPathThatNoLongerExists_ReturnsNull()
    {
        Tracks(TestData.CreateTrack());

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.Category([9]), true));
    }

    [Fact]
    public void TrackTheListDoesNotKnow_IsNeverPicked()
    {
        // An unresolved track has no dance to be weighted by, so it cannot take part.
        Tracks(TestData.CreateTrack("An Tri dipop", slug: null));

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true));
    }

    [Fact]
    public void DanceWithNoTracks_IsSkipped()
    {
        Tracks(TestData.CreateTrack("Plinn"));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true);

        Assert.NotNull(result);
        Assert.Equal("plinn", result.DanceSlug);
    }

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        _danceListStore.Current.Returns(DanceList.Empty);
        Tracks(TestData.CreateTrack());

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true));
    }

    [Fact]
    public void NoTracks_ReturnsNull()
    {
        Tracks();

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true));
    }

    [Fact]
    public void DanceWeightedZero_IsNeverPicked()
    {
        _danceListStore.Current.Returns(new DanceList
        {
            Categories =
            [
                TestData.CreateCategory("Common", dances:
                [
                    TestData.CreateDance("mazurka", 0, "Mazurka"),
                    TestData.CreateDance("plinn", 1, "Plinn")
                ])
            ]
        });
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal("plinn", _sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true)?.DanceSlug);
        }
    }

    [Fact]
    public void CategoryWeightedZero_TakesEverythingUnderItWithIt()
    {
        _danceListStore.Current.Returns(new DanceList
        {
            Categories =
            [
                TestData.CreateCategory("Silent", 0, dances: [TestData.CreateDance("mazurka", 1, "Mazurka")]),
                TestData.CreateCategory("Heard", 1, dances: [TestData.CreateDance("plinn", 1, "Plinn")])
            ]
        });
        Tracks(TestData.CreateTrack(), TestData.CreateTrack("Plinn"));

        for (var i = 0; i < 40; i++)
        {
            Assert.Equal("plinn", _sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true)?.DanceSlug);
        }
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

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), false));
    }

    [Fact]
    public void QueuedTrack_IsExcludedWhenDuplicatesAreNotAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _queueService.Items.Returns(new List<IQueueItem> { new TrackQueueItem(track, false) });

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), false));
    }

    [Fact]
    public void PlayingTrack_IsExcludedWhenDuplicatesAreNotAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _consumptionService.CurrentItem.Returns(new TrackQueueItem(track, false));

        Assert.Null(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), false));
    }

    [Fact]
    public void ExcludedTrack_IsStillPickedWhenDuplicatesAreAllowed()
    {
        var track = TestData.CreateTrack();
        Tracks(track);
        _consumptionService.CurrentItem.Returns(new TrackQueueItem(track, false));

        Assert.NotNull(_sut.PickRandomTrack(new RandomSelectionScope.EntireList(), true));
    }

    private void Tracks(params Track[] tracks) => _trackStore.Current.Returns(tracks.ToList());
}
