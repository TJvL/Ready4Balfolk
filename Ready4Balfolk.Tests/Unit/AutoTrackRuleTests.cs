using System.IO.Abstractions.TestingHelpers;
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
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        var verdict = sut.EvaluateAdd(auto, []);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_AutoTrack_QueueHasRequests_NoOpinion()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        var existing = new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false);
        var verdict = sut.EvaluateAdd(auto, [existing]);
        Assert.Null(verdict);
    }

    [Fact]
    public void EvaluateAdd_AutoTrack_AlreadyPresent_Denies()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        var existing = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), true));
        var verdict = sut.EvaluateAdd(auto, [existing]);
        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
    }

    [Fact]
    public void EvaluateAdd_Regular_NoOpinion()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        var verdict = sut.EvaluateAdd(track, []);
        Assert.Null(verdict);
    }

    // --- GetPreAddRemovalPredicate ---

    [Fact]
    public void GetPreAddRemovalPredicate_Regular_ReturnsNull()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "X"), true));

        // The auto-track survives a request being added; it is only moved to the bottom.
        Assert.Null(sut.GetPreAddRemovalPredicate(track, [auto]));
    }

    [Fact]
    public void GetPreAddRemovalPredicate_AutoTrack_ReturnsNull()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        var predicate = sut.GetPreAddRemovalPredicate(auto, []);
        Assert.Null(predicate);
    }

    // --- CanRemove ---

    [Fact]
    public void CanRemove_AutoTrack_False()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        Assert.False(sut.CanRemove(auto));
    }

    [Fact]
    public void CanRemove_Regular_Null()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        Assert.Null(sut.CanRemove(track));
    }

    // --- CanClear ---

    [Fact]
    public void CanClear_OnlyAutoTracks_False()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        Assert.False(sut.CanClear([auto]));
    }

    [Fact]
    public void CanClear_HasRegular_Null()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        Assert.Null(sut.CanClear([track]));
    }

    // --- GetEvictionIndices ---

    [Fact]
    public void GetEvictionIndices_Enabled_ReturnsEmpty()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(true);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false), new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "B"), true))
        };
        Assert.Empty(sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_Disabled_ReturnsAutoTrackIndices()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(false);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false), new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "B"), true))
        };
        Assert.Equal([1], sut.GetEvictionIndices(items));
    }

    [Fact]
    public void GetEvictionIndices_Disabled_NoAutoTracks_ReturnsEmpty()
    {
        var mockFileSystem = new MockFileSystem();

        var sut = new AutoTrackRule(false);
        var items = new List<IQueueItem>
        {
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false)
        };
        Assert.Empty(sut.GetEvictionIndices(items));
    }
}
