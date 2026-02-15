using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class AutoTrackRuleTests
{
    // --- EvaluateAdd ---

    [Fact]
    public void EvaluateAdd_AutoTrack_EmptyQueue_NoOpinion()
    {
        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        var verdict = sut.EvaluateAdd(auto, []);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_AutoTrack_NonEmpty_Denies()
    {
        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        var existing = new TrackQueueItem(TestData.CreateTrack("A"), false);
        var verdict = sut.EvaluateAdd(auto, [existing]);
        Assert.NotNull(verdict);
        Assert.False(verdict!.Allowed);
    }

    [Fact]
    public void EvaluateAdd_Regular_NoOpinion()
    {
        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        var verdict = sut.EvaluateAdd(track, []);
        Assert.Null(verdict);
    }

    // --- GetPreAddRemovalPredicate ---

    [Fact]
    public void GetPreAddRemovalPredicate_Regular_ReturnsAutoTrackPredicate()
    {
        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("X"), true));
        var predicate = sut.GetPreAddRemovalPredicate(track, [auto]);

        Assert.NotNull(predicate);
        Assert.True(predicate!(auto));
        Assert.False(predicate(track));
    }

    [Fact]
    public void GetPreAddRemovalPredicate_AutoTrack_ReturnsNull()
    {
        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        var predicate = sut.GetPreAddRemovalPredicate(auto, []);
        Assert.Null(predicate);
    }

    // --- CanRemove ---

    [Fact]
    public void CanRemove_AutoTrack_False()
    {
        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.False(sut.CanRemove(auto));
    }

    [Fact]
    public void CanRemove_Regular_Null()
    {
        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.Null(sut.CanRemove(track));
    }

    // --- CanClear ---

    [Fact]
    public void CanClear_OnlyAutoTracks_False()
    {
        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(), true));
        Assert.False(sut.CanClear([auto]));
    }

    [Fact]
    public void CanClear_HasRegular_Null()
    {
        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(), false);
        Assert.Null(sut.CanClear([track]));
    }

    // --- GetEvictionIndices ---

    [Fact]
    public void GetEvictionIndices_Enabled_ReturnsEmpty()
    {
        var sut = new AutoTrackRule(true);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("B"), true))
        };
        Assert.Empty(sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_Disabled_ReturnsAutoTrackIndices()
    {
        var sut = new AutoTrackRule(false);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack("A"), false),
            new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack("B"), true))
        };
        Assert.Equal([1], sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_Disabled_NoAutoTracks_ReturnsEmpty()
    {
        var sut = new AutoTrackRule(false);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack("A"), false)
        };
        Assert.Empty(sut.GetEvictionIndices(items));
    }
}
