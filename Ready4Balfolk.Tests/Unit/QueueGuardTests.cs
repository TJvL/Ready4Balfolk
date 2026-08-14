using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class QueueGuardTests
{
    private static QueueGuard CreateGuard(int maxItems = 6, bool allowDuplicates = true)
    {
        var historyStore = Substitute.For<IQueueHistoryStore>();
        historyStore.Current.Returns(new QueueHistory(null, []));
        var rules = new List<IQueueRule>
        {
            new AutoTrackRule(true)
        };
        if (!allowDuplicates)
        {
            rules.Add(new DuplicateTrackRule(() => null, historyStore));
        }

        rules.Add(new MaxItemsRule(maxItems));
        return new QueueGuard(rules);
    }

    [Fact]
    public void EvaluateAdd_Regular_UnderLimit_Allows()
    {
        var mockFileSystem = new MockFileSystem();

        var guard = CreateGuard(maxItems: 3);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);

        var result = guard.EvaluateAdd(track, []);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void EvaluateAdd_Regular_AtLimit_Denies()
    {
        var mockFileSystem = new MockFileSystem();

        var guard = CreateGuard(maxItems: 2);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "New"), false);
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false),
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "B"), false)
        ];

        var result = guard.EvaluateAdd(track, items);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void EvaluateAdd_Regular_AtLimitWithAutoTrack_AutoTrackDoesNotCount()
    {
        var mockFileSystem = new MockFileSystem();

        // max=2, queue has 1 real + 1 auto. The auto-track does not count, so 1 < 2 → allowed,
        // and nothing is removed to make room.
        var guard = CreateGuard(maxItems: 2);
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "New"), false);
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false),
            new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "B"), true))
        ];

        var result = guard.EvaluateAdd(track, items);
        Assert.True(result.Allowed);
        Assert.Null(result.RemovalPredicate);
    }

    [Fact]
    public void EvaluateAdd_AutoTrack_QueueHasRequests_Allows()
    {
        var mockFileSystem = new MockFileSystem();

        var guard = CreateGuard();
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        IReadOnlyList<IQueueItem> items = [new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), false)];

        var result = guard.EvaluateAdd(auto, items);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void EvaluateAdd_AutoTrack_AlreadyPresent_Denies()
    {
        var mockFileSystem = new MockFileSystem();
        var guard = CreateGuard();
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        IReadOnlyList<IQueueItem> items =
            [new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "A"), true))];

        var result = guard.EvaluateAdd(auto, items);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void CanRemove_FirstDenyWins()
    {
        var mockFileSystem = new MockFileSystem();

        var guard = CreateGuard();
        var auto = new AutoTrackQueueItem(new TrackQueueItem(TestData.CreateTrack(mockFileSystem), true));
        Assert.False(guard.CanRemove(auto));

        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        Assert.True(guard.CanRemove(track));
    }

    [Fact]
    public void CanClear_DefaultsToTrue()
    {
        var mockFileSystem = new MockFileSystem();

        var guard = CreateGuard();
        var track = new TrackQueueItem(TestData.CreateTrack(mockFileSystem), false);
        Assert.True(guard.CanClear([track]));
    }

    // --- GetEvictionIndices ---

    [Fact]
    public void GetEvictionIndices_CombinesRules()
    {
        var mockFileSystem = new MockFileSystem();

        // max=2, no duplicates, queue has 3 items with a duplicate
        var guard = CreateGuard(maxItems: 2, allowDuplicates: false);
        var track = TestData.CreateTrack(mockFileSystem);
        IReadOnlyList<IQueueItem> items =
        [
            new TrackQueueItem(track, false),
            new TrackQueueItem(track, false), // duplicate at index 1
            new TrackQueueItem(TestData.CreateTrack(mockFileSystem, "B"), false) // over limit at index 2
        ];

        var indices = guard.GetEvictionIndices(items);
        // Should contain both 1 (duplicate) and 2 (over limit), sorted descending
        Assert.Contains(1, indices);
        Assert.Contains(2, indices);
        Assert.True(indices[0] > indices[1]); // descending order
    }
}
