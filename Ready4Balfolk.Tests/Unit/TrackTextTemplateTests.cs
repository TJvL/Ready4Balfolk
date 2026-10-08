using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What a template does to a track, and what it does to the fields a library is missing.
/// </summary>
/// <remarks>
/// The separators are the whole of it. Every real library has a track with no title on it, and a
/// screen full of "Naragonia - " is what a template is judged on rather than the happy case.
/// </remarks>
public sealed class TrackTextTemplateTests
{
    [Theory]
    [InlineData("%d - %a - %t", "Mazurka - Naragonia - Salamandre")]
    [InlineData("%a - %t", "Naragonia - Salamandre")]
    [InlineData("%t (%d)", "Salamandre (Mazurka)")]
    [InlineData("%d", "Mazurka")]
    [InlineData("%t / %a / %d", "Salamandre / Naragonia / Mazurka")]
    public void ATemplate_WritesTheFieldsWhereItSaysToWriteThem(string template, string expected) =>
        Assert.Equal(expected, TrackTextTemplate.Render(template, "Mazurka", "Naragonia", "Salamandre"));

    [Fact]
    public void AFieldWithNothingInIt_TakesItsSeparatorWithIt() =>
        Assert.Equal("Naragonia", TrackTextTemplate.Render("%a - %t", "Mazurka", "Naragonia", ""));

    [Fact]
    public void AMissingFieldInTheMiddle_LeavesOneSeparatorRatherThanTwo() =>
        Assert.Equal(
            "Mazurka - Salamandre",
            TrackTextTemplate.Render("%d - %a - %t", "Mazurka", "", "Salamandre"));

    [Fact]
    public void AMissingFieldAtTheFront_DoesNotOpenTheLineWithAtSeparator() =>
        Assert.Equal(
            "Salamandre",
            TrackTextTemplate.Render("%a - %t", "Mazurka", "", "Salamandre"));

    [Theory]
    [InlineData("%t (%d)", "", "Naragonia", "Salamandre", "Salamandre")]
    [InlineData("%t (%d)", "Mazurka", "Naragonia", "", "(Mazurka)")]
    [InlineData("(%d) %t", "", "Naragonia", "Salamandre", "Salamandre")]
    [InlineData("(%d) %t", "Mazurka", "Naragonia", "", "(Mazurka)")]
    [InlineData("%a - %t [%d]", "", "Naragonia", "Salamandre", "Naragonia - Salamandre")]
    [InlineData("%a - %t [%d]", "Mazurka", "Naragonia", "", "Naragonia [Mazurka]")]
    [InlineData("%t (%d) [%a]", "", "Naragonia", "Salamandre", "Salamandre [Naragonia]")]
    [InlineData("%t (%d) [%a]", "Mazurka", "", "Salamandre", "Salamandre (Mazurka)")]
    [InlineData("%t (by %a)", "Mazurka", "", "Salamandre", "Salamandre")]
    [InlineData("%t [(%d)]", "", "Naragonia", "Salamandre", "Salamandre")]
    [InlineData("%t [(%d)]", "Mazurka", "Naragonia", "Salamandre", "Salamandre [(Mazurka)]")]
    public void ABracketedFieldWithNothingInIt_TakesBothHalvesOfItsBracketWithIt(
        string template,
        string dance,
        string artist,
        string title,
        string expected) =>
        Assert.Equal(expected, TrackTextTemplate.Render(template, dance, artist, title));

    [Fact]
    public void ABracketThatNeverCloses_IsLeftWhereItWasWritten() =>
        Assert.Equal("Salamandre (Mazurka", TrackTextTemplate.Render("%t (%d", "Mazurka", "", "Salamandre"));

    [Fact]
    public void ATrackThatSaysNothingTheTemplateAsksFor_WritesNothingAtAll() =>
        Assert.Equal("", TrackTextTemplate.Render("%a - %t", "Mazurka", "", ""));

    [Fact]
    public void APlaceholderNobodyHasHeardOf_IsLeftOnScreenRatherThanSwallowed() =>
        Assert.Equal(
            "%z Mazurka",
            TrackTextTemplate.Render("%z %d", "Mazurka", "Naragonia", "Salamandre"));

    [Fact]
    public void APerCentSignSomebodyMeant_Survives() =>
        Assert.Equal("100% Mazurka", TrackTextTemplate.Render("100%% %d", "Mazurka", "", ""));

    [Fact]
    public void ATemplateWithNothingInIt_WritesNothing() =>
        Assert.Equal("", TrackTextTemplate.Render("", "Mazurka", "Naragonia", "Salamandre"));

    [Fact]
    public void ATrack_IsWrittenFromItsOwnFields()
    {
        var track = TestData.CreateTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre");

        Assert.Equal("Mazurka: Salamandre", TrackTextTemplate.Render("%d: %t", track));
    }

    [Fact]
    public void AQueuedTrackWithNoArtist_IsNamedWithoutTheEmptySeparator()
    {
        // What the error bar says when a file will not play. Built by hand, it read
        // "Mazurka -  - Salamandre", the one case every other surface avoids.
        var track = TestData.CreateTrack(dance: "Mazurka", artist: "", title: "Salamandre");

        Assert.Equal("Mazurka - Salamandre", new TrackQueueItem(track, false).Description);
    }
}
