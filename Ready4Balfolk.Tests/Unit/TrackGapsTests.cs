using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// How much quiet a queue holds, which is what anything saying when the evening finishes has to add.
/// </summary>
public sealed class TrackGapsTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(10);

    [Fact]
    public void BetweenTwoDances_ThereIsOneGap() =>
        Assert.Equal(Gap, TrackGaps.Between(Track(), [Track()], Gap));

    [Fact]
    public void ThreeDancesInARow_HaveAGapBetweenEachPair() =>
        Assert.Equal(Gap * 3, TrackGaps.Between(Track(), [Track(), Track(), Track()], Gap));

    [Fact]
    public void ADelayInTheMiddle_TakesTheGapsAroundItWithIt()
    {
        // The delay is already the room being given time, on both sides of it.
        var queued = new IQueueItem[] { new DelayQueueItem(TimeSpan.FromSeconds(30)), Track() };

        Assert.Equal(TimeSpan.Zero, TrackGaps.Between(Track(), queued, Gap));
    }

    [Fact]
    public void WithNothingPlaying_TheFirstDanceIsNotWaitedFor() =>
        Assert.Equal(TimeSpan.Zero, TrackGaps.Between(null, [Track()], Gap));

    [Fact]
    public void TheEndOfTheNight_IsNotWaitedFor() =>
        Assert.Equal(
            TimeSpan.Zero,
            TrackGaps.Between(Track(), [new EndOfNightQueueItem("/music/closing.mp3", null)], Gap));

    [Fact]
    public void AnAutoQueuedTrack_IsADanceLikeAnyOther() =>
        Assert.Equal(
            Gap,
            TrackGaps.Between(Track(), [new AutoTrackQueueItem(Track())], Gap));

    [Fact]
    public void SwitchedOff_ThereIsNothingToAdd() =>
        Assert.Equal(TimeSpan.Zero, TrackGaps.Between(Track(), [Track(), Track()], TimeSpan.Zero));

    private static TrackQueueItem Track() => new(TestData.CreateTrack(), false);
}
