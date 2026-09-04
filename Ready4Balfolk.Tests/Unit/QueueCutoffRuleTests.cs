using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class QueueCutoffRuleTests
{
    private static readonly DateTime Evening = new(2026, 8, 6, 22, 45, 0, DateTimeKind.Local);

    private static QueueCutoffRule CreateSut(
        int cutoffHour = 23, int graceMinutes = 2, TimeSpan? currentRemaining = null, DateTime? now = null,
        IQueueItem? current = null, TimeSpan? gapBetweenTracks = null)
        => new(TimeSpan.FromHours(cutoffHour), TimeSpan.FromMinutes(graceMinutes),
            () => current, () => currentRemaining ?? TimeSpan.Zero, () => now ?? Evening,
            gapBetweenTracks ?? TimeSpan.Zero);

    private static TrackQueueItem Track(int minutes) =>
        new(TestData.CreateTrack(lengthSeconds: minutes * 60), false);

    private static AutoTrackQueueItem Auto(int minutes) =>
        new(new TrackQueueItem(TestData.CreateTrack(lengthSeconds: minutes * 60), true));

    // 22:45 + 5 minutes is nowhere near 23:00.
    [Fact]
    public void EvaluateAdd_WellInsideCutoff_NoOpinion() =>
        Assert.Null(CreateSut().EvaluateAdd(Track(5), []));

    [Fact]
    public void EvaluateAdd_PastCutoff_Denies()
    {
        // 22:45 + 10 + 10 = 23:05, past 23:00 plus 2 minutes of grace.
        var verdict = CreateSut().EvaluateAdd(Track(10), [Track(10)]);

        Assert.NotNull(verdict);
        Assert.False(verdict.Allowed);
        Assert.Equal(QueueDenial.Cutoff, verdict.Denial);
        Assert.Contains("23:00", verdict.Reason!, StringComparison.Ordinal);
    }

    // 22:45 + 16 = 23:01, past the cutoff but inside the 2 minute grace.
    [Fact]
    public void EvaluateAdd_WithinGrace_Allowed() =>
        Assert.Null(CreateSut().EvaluateAdd(Track(16), []));

    [Fact]
    public void EvaluateAdd_CountsTheCurrentlyPlayingRemainder()
    {
        // 4 minutes left of the current track pushes 22:45 + 4 + 14 to 23:03.
        var sut = CreateSut(currentRemaining: TimeSpan.FromMinutes(4));

        Assert.NotNull(sut.EvaluateAdd(Track(14), []));
    }

    [Fact]
    public void EvaluateAdd_StopQueued_NoOpinion()
    {
        // A stop has no knowable length, so there is no end time to judge against.
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [Track(10), new StopQueueItem(), Track(30)];

        Assert.Null(sut.EvaluateAdd(Track(30), queued));
    }

    [Fact]
    public void EvaluateAdd_OpenEndedMessageQueued_NoOpinion()
    {
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [new MessageQueueItem("Speech", null)];

        Assert.Null(sut.EvaluateAdd(Track(60), queued));
    }

    [Fact]
    public void EvaluateAdd_TimedMessageStillCounts()
    {
        // A timed pause is knowable, so it counts towards the projection like anything else.
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [new MessageQueueItem("Notice", TimeSpan.FromMinutes(10))];

        Assert.NotNull(sut.EvaluateAdd(Track(10), queued));
    }

    [Fact]
    public void EvaluateAdd_ControlItemsAreNeverRefused()
    {
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [Track(60)];

        Assert.Null(sut.EvaluateAdd(new StopQueueItem(), queued));
        Assert.Null(sut.EvaluateAdd(new MessageQueueItem("Speech", null), queued));
    }

    [Fact]
    public void EvaluateAdd_EndOfNightIsNeverRefused()
    {
        // It is what happens after the cutoff, not something trying to sneak past it.
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [Track(60)];

        Assert.Null(sut.EvaluateAdd(new EndOfNightQueueItem("/audio/last-waltz.mp3", TimeSpan.FromMinutes(4)), queued));
    }

    [Fact]
    public void EvaluateAdd_AutoTrackPastCutoff_Denies()
    {
        // The auto-track is what keeps the queue refilling itself, so exempting it would leave the
        // evening running past midnight while the setting claims it ends at 23:00.
        var sut = CreateSut();

        Assert.NotNull(sut.EvaluateAdd(Auto(10), [Track(10)]));
    }

    [Fact]
    public void EvaluateAdd_AutoTrackInsideCutoff_NoOpinion() =>
        Assert.Null(CreateSut().EvaluateAdd(Auto(5), []));

    [Fact]
    public void EvaluateAdd_AutoTrackPastCutoffWithStopQueued_NoOpinion()
    {
        // Nothing is refused past a halt, and the auto-track is no exception.
        var sut = CreateSut();
        IReadOnlyList<IQueueItem> queued = [new StopQueueItem()];

        Assert.Null(sut.EvaluateAdd(Auto(60), queued));
    }

    [Fact]
    public void EvaluateAdd_AfterMidnight_JudgesAgainstTheEveningInProgress()
    {
        // 00:30 with a 23:00 cutoff: the evening has run past midnight, so the limit is behind us
        // and anything further is refused rather than being measured against tomorrow night.
        var sut = CreateSut(now: new DateTime(2026, 8, 7, 0, 30, 0, DateTimeKind.Local));

        Assert.NotNull(sut.EvaluateAdd(Track(5), []));
    }
}
