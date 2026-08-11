using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Covers the mapping both presentation surfaces read. The desktop window and the browser draw six
/// different pictures from it, so a wrong kind here is wrong in two places at once.
/// </summary>
public sealed class PresentationStateServiceTests
{
    // --- Map ---

    [Fact]
    public void Map_Null_IsNone()
    {
        var item = PresentationStateService.Map(null);

        Assert.Equal(PresentationItemKind.None, item.Kind);
        Assert.False(item.HasContent);
    }

    [Fact]
    public void Map_Track_CarriesDanceArtistAndTitle()
    {
        var track = new TrackQueueItem(TestData.CreateTrack("Scottish", "Naragonia", "Sur le Pont"), false);

        var item = PresentationStateService.Map(track);

        Assert.Equal(PresentationItemKind.Track, item.Kind);
        Assert.Equal("Scottish", item.Primary);
        Assert.Equal("Naragonia", item.Artist);
        Assert.Equal("Sur le Pont", item.Title);
        Assert.True(item.HasSubtitle);
    }

    [Fact]
    public void Map_AutoTrack_IsIndistinguishableFromATrack()
    {
        var track = new TrackQueueItem(TestData.CreateTrack("An Dro", "Sowieso", "Kleine An Dro"), true);

        var item = PresentationStateService.Map(new AutoTrackQueueItem(track));

        Assert.Equal(PresentationItemKind.Track, item.Kind);
        Assert.Equal("An Dro", item.Primary);
        Assert.Equal("Sowieso", item.Artist);
    }

    [Fact]
    public void Map_TrackWithoutTitle_HasNoTitleAndStillHasASubtitle()
    {
        var track = new TrackQueueItem(TestData.CreateTrack("Cercle Circassien", "Bal O'Gadjo", ""), false);

        var item = PresentationStateService.Map(track);

        Assert.Equal("", item.Title);
        Assert.True(item.HasSubtitle);
    }

    [Fact]
    public void Map_Message_PutsTheTextInThePrimaryLine()
    {
        var item = PresentationStateService.Map(new MessageQueueItem("Bar closes at midnight"));

        Assert.Equal(PresentationItemKind.Message, item.Kind);
        Assert.Equal("Bar closes at midnight", item.Primary);
        Assert.Equal("", item.Artist);
        Assert.False(item.HasSubtitle);
    }

    [Fact]
    public void Map_Delay_CarriesNoText()
    {
        // Deliberately empty: the desktop window says it in UiStrings and the browser in its own
        // strings, so Domain must not pick one of them.
        var item = PresentationStateService.Map(new DelayQueueItem(TimeSpan.FromSeconds(30)));

        Assert.Equal(PresentationItemKind.Delay, item.Kind);
        Assert.Equal("", item.Primary);
    }

    [Fact]
    public void Map_Stop_CarriesNoText()
    {
        var item = PresentationStateService.Map(new StopQueueItem());

        Assert.Equal(PresentationItemKind.Stop, item.Kind);
        Assert.Equal("", item.Primary);
    }

    // --- PresentationProgress ---

    [Fact]
    public void Progress_RemainingNeverGoesNegative()
    {
        var progress = new PresentationProgress(TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(180));

        Assert.Equal(TimeSpan.Zero, progress.Remaining);
    }

    [Fact]
    public void Progress_WithoutADuration_HasNoFraction()
    {
        // A stop has no end time, so the bar has nothing to draw rather than sitting at zero.
        var progress = new PresentationProgress(TimeSpan.FromSeconds(42), TimeSpan.Zero);

        Assert.Equal(0d, progress.Fraction);
        Assert.Equal(TimeSpan.Zero, progress.Remaining);
    }

    [Fact]
    public void Progress_Halfway_IsHalf()
    {
        var progress = new PresentationProgress(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(180));

        Assert.Equal(0.5d, progress.Fraction, 3);
    }
}
