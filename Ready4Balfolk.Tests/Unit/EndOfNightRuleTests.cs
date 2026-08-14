using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class EndOfNightRuleTests
{
    private static readonly EndOfNightQueueItem EndOfNight = new("/audio/last-waltz.mp3", TimeSpan.FromMinutes(4));

    private static EndOfNightRule CreateSut(IQueueItem? currentItem = null) => new(() => currentItem);

    private static TrackQueueItem Track() => new(TestData.CreateTrack(), false);

    private static AutoTrackQueueItem Auto() => new(new TrackQueueItem(TestData.CreateTrack(), true));

    [Fact]
    public void EvaluateAdd_EveningStillOpen_NoOpinion() =>
        Assert.Null(CreateSut().EvaluateAdd(Track(), [Track()]));

    [Theory]
    [MemberData(nameof(EverythingThatCouldBeAdded))]
    public void EvaluateAdd_EndOfNightQueued_RefusesEverything(IQueueItem item)
    {
        var verdict = CreateSut().EvaluateAdd(item, [EndOfNight]);

        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
        Assert.Equal(QueueDenial.EveningEnded, verdict.Denial);
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason));
    }

    public static TheoryData<IQueueItem> EverythingThatCouldBeAdded() =>
    [
        Track(),
        Auto(),
        new DelayQueueItem(TimeSpan.FromSeconds(30)),
        new MessageQueueItem("Bar closes at midnight"),
        new StopQueueItem(),
        EndOfNight
    ];

    [Fact]
    public void EvaluateAdd_EndOfNightPlaying_StillRefuses()
    {
        // The queue would otherwise reopen the moment the closing music started, and the auto-queue
        // would put a dance behind it.
        var verdict = CreateSut(EndOfNight).EvaluateAdd(Track(), []);

        Assert.NotNull(verdict);
        Assert.Equal(QueueDenial.EveningEnded, verdict.Denial);
    }

    [Fact]
    public void EvaluateAdd_EndOfNightRemoved_EveningReopens() =>
        Assert.Null(CreateSut().EvaluateAdd(Track(), [Track()]));

    [Fact]
    public void GetPreAddRemovalPredicate_EndOfNight_TakesTheAutoTrackWithIt()
    {
        var predicate = CreateSut().GetPreAddRemovalPredicate(EndOfNight, [Auto()]);

        Assert.NotNull(predicate);
        Assert.True(predicate(Auto()));
        Assert.False(predicate(Track()));
    }

    [Fact]
    public void GetPreAddRemovalPredicate_AnythingElse_LeavesTheQueueAlone() =>
        Assert.Null(CreateSut().GetPreAddRemovalPredicate(Track(), [Auto()]));

    [Fact]
    public void CanRemove_EndOfNight_Allowed() =>
        // Changing your mind is not an error state.
        Assert.Null(CreateSut().CanRemove(EndOfNight));

    [Fact]
    public void CanMove_EndOfNight_Refused() =>
        Assert.False(CreateSut().CanMove(EndOfNight));

    [Fact]
    public void CanMove_AnythingElse_NoOpinion() =>
        Assert.Null(CreateSut().CanMove(Track()));

    [Fact]
    public void GetEvictionIndices_NeverEvicts() =>
        Assert.Empty(CreateSut().GetEvictionIndices([Track(), EndOfNight]));
}
