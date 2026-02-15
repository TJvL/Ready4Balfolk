using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class MaxItemsRuleTests
{
    private readonly MaxItemsRule _sut = new(3);

    [Fact]
    public void EvaluateAdd_UnderLimit_NoOpinion()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        var verdict = _sut.EvaluateAdd(track, [new TrackQueueItem(TestData.CreateTrack("A"), false)]);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_AtLimit_Denies()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new TrackQueueItem(TestData.CreateTrack("B"), false),
            new TrackQueueItem(TestData.CreateTrack("C"), false)
        ];
        var verdict = _sut.EvaluateAdd(track, items);
        Assert.NotNull(verdict);
        Assert.False(verdict!.Allowed);
        Assert.Contains("3", verdict.Reason!);
    }

    [Fact]
    public void GetPreAddRemovalPredicate_AlwaysNull()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.Null(_sut.GetPreAddRemovalPredicate(track, []));
    }

    [Fact]
    public void CanRemove_AlwaysNull()
    {
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.Null(_sut.CanRemove(track));
    }

    // --- GetEvictionIndices ---

    [Fact]
    public void GetEvictionIndices_UnderLimit_ReturnsEmpty()
    {
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new TrackQueueItem(TestData.CreateTrack("B"), false)
        ];
        Assert.Empty(_sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_OverLimit_ReturnsTailIndices()
    {
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new TrackQueueItem(TestData.CreateTrack("B"), false),
            new TrackQueueItem(TestData.CreateTrack("C"), false),
            new TrackQueueItem(TestData.CreateTrack("D"), false),
            new TrackQueueItem(TestData.CreateTrack("E"), false)
        ];
        var indices = _sut.GetEvictionIndices(items);
        Assert.Equal([3, 4], indices);
    }
}
