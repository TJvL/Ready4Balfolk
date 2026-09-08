using System.Globalization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.History;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The evening as a rights organisation receives it.</summary>
public sealed class NightReportTests
{
    private static readonly DateTime Evening = new(2026, 9, 5, 20, 30, 0, DateTimeKind.Local);

    [Fact]
    public void Render_SaysWhoPlayedWhatAndWhen()
    {
        var document = NightReport.Render(Night(
            Track("Naragonia", "Salamandre", Evening),
            Track("Trio Loubelya", "Bourrée des Alpes", Evening.AddMinutes(4))));

        Assert.Contains("Naragonia", document, StringComparison.Ordinal);
        Assert.Contains("Salamandre", document, StringComparison.Ordinal);
        Assert.Contains("Trio Loubelya", document, StringComparison.Ordinal);
        Assert.Contains("20:30", document, StringComparison.Ordinal);
        Assert.Contains("20:34", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PutsTheTracksInTheOrderTheyWerePlayed()
    {
        var document = NightReport.Render(Night(
            Track("First", "One", Evening),
            Track("Second", "Two", Evening.AddMinutes(4))));

        Assert.True(
            document.IndexOf("First", StringComparison.Ordinal) <
            document.IndexOf("Second", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_HeadsTheDocumentWithTheEveningAndItsColumns()
    {
        var document = NightReport.Render(Night(Track("Naragonia", "Salamandre", Evening)));

        Assert.Contains(DomainStrings.NightReport_Heading, document, StringComparison.Ordinal);
        Assert.Contains(Evening.ToString("D", CultureInfo.CurrentCulture), document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_TimeColumn, document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_ArtistColumn, document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_TitleColumn, document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_IsAnRtfDocumentAWordProcessorWillOpen()
    {
        var document = NightReport.Render(Night(Track("Naragonia", "Salamandre", Evening)));

        Assert.StartsWith(@"{\rtf1", document, StringComparison.Ordinal);
        Assert.EndsWith("}\n", document, StringComparison.Ordinal);

        // Every brace the document opens is one it closes, or nothing will open it at all.
        Assert.Equal(document.Count(c => c == '{'), document.Count(c => c == '}'));
    }

    [Fact]
    public void Render_LeavesOutEverythingThatIsNotATrack()
    {
        var document = NightReport.Render(new QueueHistory(Evening, [
            new MessageHistoryEntry("Last dance in ten minutes", null, CompletionStatus.Finished, Evening),
            new StopHistoryEntry(CompletionStatus.Finished, Evening.AddMinutes(1)),
            Track("Naragonia", "Salamandre", Evening.AddMinutes(2))
        ]));

        Assert.Contains("Salamandre", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Last dance", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LeavesOutATrackThatNeverPlayed()
    {
        var document = NightReport.Render(new QueueHistory(Evening, [
            Track("Naragonia", "Salamandre", Evening),
            Track("Missing", "Never Heard", Evening.AddMinutes(4)) with
            {
                CompletionStatus = CompletionStatus.FileMissing
            }
        ]));

        Assert.Contains("Salamandre", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Never Heard", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_KeepsWhereTheFileWasOffTheDocument()
    {
        var document = NightReport.Render(Night(Track("Naragonia", "Salamandre", Evening)));

        Assert.DoesNotContain("mazurka.mp3", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WritesAnAccentAsAnEscapeRatherThanAsAByteRtfCannotCarry()
    {
        var document = NightReport.Render(Night(Track("Arsène", "Bourrée", Evening)));

        // RTF is seven-bit: an accent that went in raw would be read as a different letter.
        Assert.DoesNotContain('è', document);
        Assert.DoesNotContain('é', document);
        Assert.Contains(@"Ars\u232?ne", document, StringComparison.Ordinal);
        Assert.Contains(@"Bourr\u233?e", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EscapesABraceInATitleRatherThanLettingItCloseTheDocument()
    {
        var document = NightReport.Render(Night(Track(@"A\B", "{Live}", Evening)));

        Assert.Contains(@"A\\B", document, StringComparison.Ordinal);
        Assert.Contains(@"\{Live\}", document, StringComparison.Ordinal);
        Assert.Equal(document.Count(c => c == '{'), document.Count(c => c == '}'));
    }

    [Fact]
    public void Render_LeavesOutAControlCharacterATagShouldNeverHaveCarried()
    {
        var document = NightReport.Render(Night(Track("Ar\u001bt", "Ti\u0008tle", Evening)));

        // A control character has no plain-text spelling in RTF, so it cannot go in raw.
        Assert.DoesNotContain('\u001b', document);
        Assert.DoesNotContain('\u0008', document);
        Assert.Contains("Art", document, StringComparison.Ordinal);
        Assert.Contains("Title", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_OfAnEveningWithNothingInItIsStillADocument()
    {
        var document = NightReport.Render(QueueHistory.Empty);

        Assert.StartsWith(@"{\rtf1", document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_Heading, document, StringComparison.Ordinal);
        Assert.EndsWith("}\n", document, StringComparison.Ordinal);
    }

    private static QueueHistory Night(params QueueHistoryEntry[] entries) =>
        new(Evening, [.. entries]);

    private static TrackHistoryEntry Track(string artist, string title, DateTime startedAt) => new(
        Path.Combine(Path.GetTempPath(), "mazurka.mp3"), "Mazurka", artist, title,
        TimeSpan.FromMinutes(3), false, CompletionStatus.Finished, startedAt);
}
