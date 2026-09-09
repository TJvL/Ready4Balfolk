using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.History;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The evening as a rights organisation imports it.</summary>
public sealed class NightSpreadsheetTests
{
    private static readonly DateTime Evening = new(2026, 9, 5, 20, 30, 0, DateTimeKind.Local);

    [Fact]
    public void Render_HeadsTheRowsWithTheSameColumnsTheReportShows()
    {
        var rows = Lines(NightSpreadsheet.Render(Night(Track("Naragonia", "Salamandre", Evening))));

        Assert.Equal(
            $"{DomainStrings.NightReport_TimeColumn},{DomainStrings.NightReport_ArtistColumn},{DomainStrings.NightReport_TitleColumn}",
            rows[0]);
    }

    [Fact]
    public void Render_WritesOneRowPerTrackInTheOrderTheyWerePlayed()
    {
        var rows = Lines(NightSpreadsheet.Render(Night(
            Track("Naragonia", "Salamandre", Evening),
            Track("Trio Loubelya", "La Belle", Evening.AddMinutes(4)))));

        Assert.Equal(3, rows.Length);
        Assert.Equal("20:30,Naragonia,Salamandre", rows[1]);
        Assert.Equal("20:34,Trio Loubelya,La Belle", rows[2]);
    }

    [Fact]
    public void Render_EndsEveryRecordTheWayASpreadsheetExpects()
    {
        var csv = NightSpreadsheet.Render(Night(Track("Naragonia", "Salamandre", Evening)));

        // RFC 4180 says CRLF, and it is the one a spreadsheet on any of the three platforms reads
        // without being told anything.
        Assert.EndsWith("\r\n", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_QuotesAFieldHoldingACommaRatherThanSplittingTheRow()
    {
        var rows = Lines(NightSpreadsheet.Render(Night(Track("Naragonia, Duo", "Salamandre", Evening))));

        // Unquoted, this band is two columns and every field after it is read one column across.
        Assert.Equal("20:30,\"Naragonia, Duo\",Salamandre", rows[1]);
    }

    [Fact]
    public void Render_DoublesAQuoteInsideAFieldRatherThanEndingIt()
    {
        var rows = Lines(NightSpreadsheet.Render(Night(Track("Naragonia", "The \"Live\" One", Evening))));

        Assert.Equal("20:30,Naragonia,\"The \"\"Live\"\" One\"", rows[1]);
    }

    [Fact]
    public void Render_LeavesOutTheSameThingsTheReportLeavesOut()
    {
        var csv = NightSpreadsheet.Render(new QueueHistory(Evening, [
            new MessageHistoryEntry("Last dance in ten minutes", null, CompletionStatus.Finished, Evening),
            Track("Missing", "Never Heard", Evening.AddMinutes(1)) with
            {
                CompletionStatus = CompletionStatus.FileMissing
            },
            Track("Naragonia", "Salamandre", Evening.AddMinutes(2))
        ]));

        Assert.Contains("Salamandre", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Last dance", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Never Heard", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("mazurka.mp3", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WritesAnAccentAsItself()
    {
        var csv = NightSpreadsheet.Render(Night(Track("Arsène", "Bourrée", Evening)));

        Assert.Contains("Arsène", csv, StringComparison.Ordinal);
        Assert.Contains("Bourrée", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_OfAnEveningWithNothingInItIsStillItsHeader()
    {
        var rows = Lines(NightSpreadsheet.Render(QueueHistory.Empty));

        Assert.Single(rows);
        Assert.Contains(DomainStrings.NightReport_ArtistColumn, rows[0], StringComparison.Ordinal);
    }

    private static string[] Lines(string csv) =>
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private static QueueHistory Night(params QueueHistoryEntry[] entries) =>
        new(Evening, [.. entries]);

    private static TrackHistoryEntry Track(string artist, string title, DateTime startedAt) => new(
        Path.Combine(Path.GetTempPath(), "mazurka.mp3"), "Mazurka", artist, title,
        TimeSpan.FromMinutes(3), false, CompletionStatus.Finished, startedAt);
}
