using System.Globalization;
using System.Text;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.History;

/// <summary>An evening written out as a document a stranger reads.</summary>
/// <remarks>
/// <para>
/// The JSON export is the night as the application holds it, and the spreadsheet is the night as
/// something else imports it. This one is what gets handed to a rights organisation to show what
/// was played, so it says a time, an artist and a title under a heading naming the evening, and
/// nothing else the night carries.
/// </para>
/// <para>
/// One HTML file, styles and all. A browser exists on every machine and every phone, which no word
/// processor does, and a mail client shows this inline where an attachment it cannot read is a
/// dead end. It prints from the browser for anyone who wanted a PDF, its table pastes into a
/// spreadsheet with the columns intact, and it is a few hundred bytes a string builder writes:
/// no dependency to install and nothing to go looking for in a hall with no internet.
/// </para>
/// <para>
/// Self-contained on purpose. A separate stylesheet would be a second file, and a second file is
/// an archive to unpack rather than a document to open.
/// </para>
/// </remarks>
public static class NightReport
{
    /// <summary>The night as an HTML document.</summary>
    public static string Render(QueueHistory night)
    {
        var document = new StringBuilder();

        document.Append("<!doctype html>\n<html lang=\"")
            .Append(CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .Append("\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n<title>");
        AppendEscaped(document, DomainStrings.NightReport_Heading);
        document.Append("</title>\n").Append(Style).Append("</head>\n<body>\n<h1>");
        AppendEscaped(document, DomainStrings.NightReport_Heading);
        document.Append("</h1>\n");

        if (night.StartedAt is { } startedAt)
        {
            document.Append("<p class=\"evening\">");
            AppendEscaped(document, startedAt.ToString("D", CultureInfo.CurrentCulture));
            document.Append("</p>\n");
        }

        document.Append("<table>\n<thead>\n<tr>");
        Cell(document, "th", DomainStrings.NightReport_TimeColumn);
        Cell(document, "th", DomainStrings.NightReport_ArtistColumn);
        Cell(document, "th", DomainStrings.NightReport_TitleColumn);
        document.Append("</tr>\n</thead>\n<tbody>\n");

        foreach (var track in Played(night))
        {
            document.Append("<tr>");
            Cell(document, "td", track.StartedAt?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? string.Empty);
            Cell(document, "td", track.Artist);
            Cell(document, "td", track.Title);
            document.Append("</tr>\n");
        }

        return document.Append("</tbody>\n</table>\n</body>\n</html>\n").ToString();
    }

    /// <summary>
    /// Enough to read on a phone and to print, and no more.
    /// </summary>
    /// <remarks>
    /// A colour scheme is left to the reader's: this is a document somebody files, not a screen in
    /// the application, and one that prints black on white whatever the machine is set to is the
    /// one a rights organisation can put away.
    /// </remarks>
    private const string Style =
        """
        <style>
        body { font-family: system-ui, sans-serif; margin: 2rem auto; max-width: 40rem; color: #111; background: #fff; }
        h1 { font-size: 1.5rem; margin-bottom: 0.25rem; }
        .evening { margin-top: 0; color: #555; }
        table { border-collapse: collapse; width: 100%; }
        th, td { text-align: left; padding: 0.35rem 0.75rem 0.35rem 0; border-bottom: 1px solid #ddd; vertical-align: top; }
        th { border-bottom-width: 2px; white-space: nowrap; }
        td:first-child { white-space: nowrap; font-variant-numeric: tabular-nums; }
        </style>

        """;

    /// <summary>The tracks the evening actually played, in the order it played them.</summary>
    /// <remarks>
    /// A track that was reached and could not be read never reached the room, and a rights
    /// organisation is being told what was heard. Everything else in a night, the messages, the
    /// pauses and the stops, is the running of the evening rather than music that was played.
    /// </remarks>
    internal static IEnumerable<TrackHistoryEntry> Played(QueueHistory night) =>
        night.Entries
            .OfType<TrackHistoryEntry>()
            .Where(track => track.CompletionStatus != CompletionStatus.FileMissing);

    private static void Cell(StringBuilder document, string tag, string text)
    {
        document.Append('<').Append(tag).Append('>');
        AppendEscaped(document, text);
        document.Append("</").Append(tag).Append('>');
    }

    /// <summary>Writes text that a reader is meant to see rather than a reader's parser.</summary>
    /// <remarks>
    /// The file is UTF-8 and says so, so an accent goes in as itself. What cannot go in as itself
    /// is the punctuation that would end the element early, and a control character, which has no
    /// spelling in HTML at all: a tag written by something that got its bytes wrong carries the odd
    /// one, so tabs and line breaks become a space and the rest are left out.
    /// </remarks>
    private static void AppendEscaped(StringBuilder document, string text)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case '&':
                    document.Append("&amp;");
                    break;
                case '<':
                    document.Append("&lt;");
                    break;
                case '>':
                    document.Append("&gt;");
                    break;
                case '"':
                    document.Append("&quot;");
                    break;
                case '\t':
                case '\r':
                case '\n':
                    document.Append(' ');
                    break;
                default:
                    if (!char.IsControl(character))
                    {
                        document.Append(character);
                    }

                    break;
            }
        }
    }
}
