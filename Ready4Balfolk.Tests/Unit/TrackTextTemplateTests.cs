using Ready4Balfolk.Domain.Services.Presentation;
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
}
