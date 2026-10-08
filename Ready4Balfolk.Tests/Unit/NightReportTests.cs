using System.Globalization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.History;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The evening as a rights organisation receives it.</summary>
public sealed class NightReportTests
{
    private static readonly DateTime Evening = new(2026, 9, 5, 20, 30, 0, DateTimeKind.Local);

    [Fact]
    public void Render_SaysWhoPlayedWhatAndWhen()
    {
        var document = NightReport.Render(Night(
            TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening),
            TestData.CreateHistoryEntry("Trio Loubelya", "Bourrée des Alpes", Evening.AddMinutes(4))));

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
            TestData.CreateHistoryEntry("First", "One", Evening),
            TestData.CreateHistoryEntry("Second", "Two", Evening.AddMinutes(4))));

        Assert.True(
            document.IndexOf("First", StringComparison.Ordinal) <
            document.IndexOf("Second", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_HeadsTheDocumentWithTheEveningAndItsColumns()
    {
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening)));
        var language = DomainStrings.Culture ?? CultureInfo.CurrentUICulture;

        Assert.Contains(DomainStrings.NightReport_Heading, document, StringComparison.Ordinal);
        Assert.Contains(Evening.ToString("D", language), document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_TimeColumn, document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_ArtistColumn, document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_TitleColumn, document, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "nl")]
    [InlineData("nl-NL", "en")]
    public void Render_IsInTheApplicationsLanguageWhateverTheMachineIsSetTo(string machine, string application)
    {
        using var cultures = new CultureScope(machine, application);

        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening)));

        // The heading and columns are in the application's language, so the document says it is
        // in that one, and its date is not in the machine's in between.
        Assert.Contains($"<html lang=\"{application}\">", document, StringComparison.Ordinal);
        Assert.Contains(Evening.ToString("D", CultureInfo.GetCultureInfo(application)), document, StringComparison.Ordinal);
        Assert.DoesNotContain(Evening.ToString("D", CultureInfo.GetCultureInfo(machine)), document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_IsOneHtmlFileABrowserWillOpen()
    {
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening)));

        Assert.StartsWith("<!doctype html>", document, StringComparison.Ordinal);
        Assert.EndsWith("</html>\n", document, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", document, StringComparison.Ordinal);

        // The styles travel inside the file. A second file to carry them is an archive to unpack
        // rather than a document to open, which is the whole reason this is one file.
        Assert.Contains("<style>", document, StringComparison.Ordinal);
        Assert.DoesNotContain("<link", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PutsTheTracksInATableSoTheyPasteIntoASpreadsheet()
    {
        var document = NightReport.Render(Night(
            TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening),
            TestData.CreateHistoryEntry("Trio Loubelya", "La Belle", Evening.AddMinutes(4))));

        // A real table rather than laid-out text: copied out of the browser it arrives in a
        // spreadsheet with the three columns still three columns.
        Assert.Equal(3, document.Split("<th>").Length - 1);
        Assert.Equal(2, document.Split("<tr>").Length - 1 - 1);
        Assert.Equal(6, document.Split("<td>").Length - 1);
    }

    [Fact]
    public void Render_LeavesOutEverythingThatIsNotATrack()
    {
        var document = NightReport.Render(new QueueHistory(Evening, [
            new MessageHistoryEntry("Last dance in ten minutes", null, CompletionStatus.Finished, Evening),
            new StopHistoryEntry(CompletionStatus.Finished, Evening.AddMinutes(1)),
            TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening.AddMinutes(2))
        ]));

        Assert.Contains("Salamandre", document, StringComparison.Ordinal);
        Assert.DoesNotContain("Last dance", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LeavesOutATrackThatNeverPlayed()
    {
        var document = NightReport.Render(new QueueHistory(Evening, [
            TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening),
            TestData.CreateHistoryEntry("Missing", "Never Heard", Evening.AddMinutes(4)) with
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
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Naragonia", "Salamandre", Evening)));

        Assert.DoesNotContain("mazurka.mp3", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WritesAnAccentAsItself()
    {
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Arsène", "Bourrée", Evening)));

        // The file is UTF-8 and says so in its own head, so a name is spelled the way it is spelled.
        Assert.Contains("Arsène", document, StringComparison.Ordinal);
        Assert.Contains("Bourrée", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EscapesMarkupInATitleRatherThanLettingItCloseTheElement()
    {
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Isaac & Nora", "<Live>", Evening)));

        Assert.Contains("Isaac &amp; Nora", document, StringComparison.Ordinal);
        Assert.Contains("&lt;Live&gt;", document, StringComparison.Ordinal);

        // The row is still a row rather than a title that opened an element of its own.
        Assert.Equal(3, document.Split("<td>").Length - 1);
    }

    [Fact]
    public void Render_LeavesOutAControlCharacterATagShouldNeverHaveCarried()
    {
        var document = NightReport.Render(Night(TestData.CreateHistoryEntry("Ar\u001bt", "Ti\u0008tle", Evening)));

        // A control character has no spelling in HTML, so it cannot go in raw.
        Assert.DoesNotContain('\u001b', document);
        Assert.DoesNotContain('\u0008', document);
        Assert.Contains("Art", document, StringComparison.Ordinal);
        Assert.Contains("Title", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_OfAnEveningWithNothingInItIsStillADocument()
    {
        var document = NightReport.Render(QueueHistory.Empty);

        Assert.StartsWith("<!doctype html>", document, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_Heading, document, StringComparison.Ordinal);
        Assert.EndsWith("</html>\n", document, StringComparison.Ordinal);
    }

    private static QueueHistory Night(params QueueHistoryEntry[] entries) =>
        new(Evening, [.. entries]);
}
