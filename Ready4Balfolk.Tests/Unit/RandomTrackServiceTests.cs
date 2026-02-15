using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class RandomTrackServiceTests
{
    private readonly IDanceTreeStore _treeStore = Substitute.For<IDanceTreeStore>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly IQueueHistoryStore _historyStore = Substitute.For<IQueueHistoryStore>();
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly IQueueConsumptionService _consumptionService = Substitute.For<IQueueConsumptionService>();
    private readonly RandomTrackService _sut;

    public RandomTrackServiceTests()
    {
        _sut = new RandomTrackService(_treeStore, _trackStore, _historyStore, _queueService, _consumptionService);
        _historyStore.Current.Returns(new QueueHistory(null, []));
        _queueService.Items.Returns(new List<IQueueItem>());
        _consumptionService.CurrentItem.Returns((IQueueItem?)null);
    }

    [Fact]
    public void EntireTree_ReturnsMatchingTrack()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);
        _trackStore.Current.Returns(new List<Track> { TestData.CreateTrack() });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), true);

        Assert.NotNull(result);
        Assert.Equal("Mazurka", result.Dance);
    }

    [Fact]
    public void Subtree_ReturnsMatchingTrack()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);
        _trackStore.Current.Returns(new List<Track>
        {
            TestData.CreateTrack(),
            TestData.CreateTrack("Bourree")
        });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.Subtree([0]), true);

        Assert.NotNull(result);
        Assert.Equal("Mazurka", result.Dance);
    }

    [Fact]
    public void SingleDance_ReturnsMatchingTrack()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);
        _trackStore.Current.Returns(new List<Track>
        {
            TestData.CreateTrack(),
            TestData.CreateTrack("Bourree")
        });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.SingleDance([0], 0), true);

        Assert.NotNull(result);
        Assert.Equal("Mazurka", result.Dance);
    }

    [Fact]
    public void NoMatchingTracks_ReturnsNull()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);
        _trackStore.Current.Returns(new List<Track> { TestData.CreateTrack("Polka") });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), true);

        Assert.Null(result);
    }

    [Fact]
    public void AllZeroWeight_ReturnsNull()
    {
        var tree = new List<Domain.Models.Tree.DanceBranch>
        {
            TestData.CreateBranch("Folk", 0, leaves: [TestData.CreateLeaf("Mazurka", 0)])
        };
        _treeStore.Current.Returns(tree);
        _trackStore.Current.Returns(new List<Track> { TestData.CreateTrack() });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), true);

        Assert.Null(result);
    }

    [Fact]
    public void NoDuplicates_ExcludesQueuedAndFinished()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);

        var mazurkaTrack = TestData.CreateTrack();
        _trackStore.Current.Returns(new List<Track> { mazurkaTrack });

        // Mazurka is in queue
        _queueService.Items.Returns(new List<IQueueItem>
        {
            new TrackQueueItem(mazurkaTrack, false)
        });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), false);

        Assert.Null(result);
    }

    [Fact]
    public void AllowDuplicates_IncludesQueuedTracks()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);

        var mazurkaTrack = TestData.CreateTrack();
        _trackStore.Current.Returns(new List<Track> { mazurkaTrack });

        _queueService.Items.Returns(new List<IQueueItem>
        {
            new TrackQueueItem(mazurkaTrack, false)
        });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), true);

        Assert.NotNull(result);
    }

    [Fact]
    public void EmptyTree_ReturnsNull()
    {
        _treeStore.Current.Returns(new List<Domain.Models.Tree.DanceBranch>());
        _trackStore.Current.Returns(new List<Track> { TestData.CreateTrack() });

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), true);

        Assert.Null(result);
    }

    [Fact]
    public void NoDuplicates_ExcludesCurrentlyPlaying()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);

        var mazurkaTrack = TestData.CreateTrack();
        _trackStore.Current.Returns(new List<Track> { mazurkaTrack });

        _consumptionService.CurrentItem.Returns(new TrackQueueItem(mazurkaTrack, false));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), false);

        Assert.Null(result);
    }

    [Fact]
    public void NoDuplicates_ExcludesFinishedHistory()
    {
        var tree = TestData.CreateSimpleTree();
        _treeStore.Current.Returns(tree);

        var mazurkaTrack = TestData.CreateTrack();
        _trackStore.Current.Returns(new List<Track> { mazurkaTrack });

        _historyStore.Current.Returns(new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry(mazurkaTrack.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]));

        var result = _sut.PickRandomTrack(new RandomSelectionScope.EntireTree(), false);

        Assert.Null(result);
    }
}
