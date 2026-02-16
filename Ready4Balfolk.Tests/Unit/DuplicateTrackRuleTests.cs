using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DuplicateTrackRuleTests
{
    private IQueueItem? _currentItem;
    private readonly IQueueHistoryStore _historyStore;
    private readonly DuplicateTrackRule _sut;

    public DuplicateTrackRuleTests()
    {
        _historyStore = Substitute.For<IQueueHistoryStore>();
        _historyStore.Current.Returns(new QueueHistory(null, []));
        _sut = new DuplicateTrackRule(() => _currentItem, _historyStore);
    }

    [Fact]
    public void EvaluateAdd_NonTrackItem_NoOpinion()
    {
        var stop = new StopQueueItem();
        var verdict = _sut.EvaluateAdd(stop, []);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_TrackAlreadyInQueue_Denies()
    {
        var track = TestData.CreateTrack();
        var item = new TrackQueueItem(track, false);
        var existing = new TrackQueueItem(track, false);

        var verdict = _sut.EvaluateAdd(item, [existing]);
        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void EvaluateAdd_TrackCurrentlyPlaying_Denies()
    {
        var track = TestData.CreateTrack();
        var item = new TrackQueueItem(track, false);
        _currentItem = new TrackQueueItem(track, false);

        var verdict = _sut.EvaluateAdd(item, []);
        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void EvaluateAdd_TrackFinishedInHistory_Denies()
    {
        var track = TestData.CreateTrack();
        var item = new TrackQueueItem(track, false);
        _historyStore.Current.Returns(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]));

        var verdict = _sut.EvaluateAdd(item, []);
        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void EvaluateAdd_TrackSkippedInHistory_NoOpinion()
    {
        var track = TestData.CreateTrack();
        var item = new TrackQueueItem(track, false);
        _historyStore.Current.Returns(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Skipped)
        ]));

        var verdict = _sut.EvaluateAdd(item, []);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_UniqueTrack_NoOpinion()
    {
        var track = TestData.CreateTrack();
        var item = new TrackQueueItem(track, false);

        var verdict = _sut.EvaluateAdd(item, []);
        Assert.Null(verdict);
    }

    [Fact]
    public void GetPreAddRemovalPredicate_AlwaysNull()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.Null(_sut.GetPreAddRemovalPredicate(track, []));
    }

    // --- GetEvictionIndices ---

    [Fact]
    public void GetEvictionIndices_NoDuplicates_ReturnsEmpty()
    {
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new TrackQueueItem(TestData.CreateTrack("B"), false)
        ];
        Assert.Empty(_sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_IntraQueueDuplicate_EvictsLater()
    {
        var track = TestData.CreateTrack();
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(track, false),
            new TrackQueueItem(TestData.CreateTrack("B"), false),
            new TrackQueueItem(track, false)
        ];
        var indices = _sut.GetEvictionIndices(items);
        Assert.Equal([2], indices);
    }

    [Fact]
    public void GetEvictionIndices_MatchesHistory_Evicts()
    {
        var track = TestData.CreateTrack();
        _historyStore.Current.Returns(new QueueHistory(null,
        [
            new TrackHistoryEntry(track.FileInfo.FullName, "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]));

        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new TrackQueueItem(track, false)
        ];
        var indices = _sut.GetEvictionIndices(items);
        Assert.Equal([1], indices);
    }

    [Fact]
    public void GetEvictionIndices_MatchesPlaying_Evicts()
    {
        var track = TestData.CreateTrack();
        _currentItem = new TrackQueueItem(track, false);

        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(track, false),
            new TrackQueueItem(TestData.CreateTrack("B"), false)
        ];
        var indices = _sut.GetEvictionIndices(items);
        Assert.Equal([0], indices);
    }
}
