using System.Globalization;
using System.Text;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.History;

/// <summary>An evening written out as a document a stranger reads.</summary>
/// <remarks>
/// <para>
/// The JSON export is the night as the application holds it. This one is what gets handed to a
/// rights organisation to show what was played, so it says a time, an artist and a title under a
/// heading naming the evening, and nothing else the night carries.
/// </para>
/// <para>
/// RTF because every word processor and every viewer on every machine opens one, and it is a few
/// hundred bytes of text a string builder writes: no dependency to install and nothing to go
/// looking for in a hall with no internet.
/// </para>
/// </remarks>
public static class NightReport
{
    /// <summary>Where the artist and the title start, in twips, so the three columns line up.</summary>
    private const string TabStops = @"\tx1000\tx4600";

    /// <summary>The night as an RTF document.</summary>
    public static string Render(QueueHistory night)
    {
        var document = new StringBuilder();
        document.Append(@"{\rtf1\ansi\ansicpg1252\uc1\deff0{\fonttbl{\f0\fswiss\fcharset0 Calibri;}}")
            .Append('\n');

        Paragraph(document, @"\pard\sa120\b\fs32 ", DomainStrings.NightReport_Heading);

        if (night.StartedAt is { } startedAt)
        {
            Paragraph(document, @"\pard\sa240\fs22 ", startedAt.ToString("D", CultureInfo.CurrentCulture));
        }

        document.Append(@"\pard").Append(TabStops).Append(@"\sa60\fs22\b ");
        AppendEscaped(document, DomainStrings.NightReport_TimeColumn);
        document.Append(@"\tab ");
        AppendEscaped(document, DomainStrings.NightReport_ArtistColumn);
        document.Append(@"\tab ");
        AppendEscaped(document, DomainStrings.NightReport_TitleColumn);
        document.Append(@"\b0\par").Append('\n');

        foreach (var track in Played(night))
        {
            document.Append(@"\pard").Append(TabStops).Append(@"\fs22 ");
            AppendEscaped(document, track.StartedAt?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? string.Empty);
            document.Append(@"\tab ");
            AppendEscaped(document, track.Artist);
            document.Append(@"\tab ");
            AppendEscaped(document, track.Title);
            document.Append(@"\par").Append('\n');
        }

        return document.Append("}\n").ToString();
    }

    /// <summary>The tracks the evening actually played, in the order it played them.</summary>
    /// <remarks>
    /// A track that was reached and could not be read never reached the room, and a rights
    /// organisation is being told what was heard. Everything else in a night, the messages, the
    /// pauses and the stops, is the running of the evening rather than music that was played.
    /// </remarks>
    private static IEnumerable<TrackHistoryEntry> Played(QueueHistory night) =>
        night.Entries
            .OfType<TrackHistoryEntry>()
            .Where(track => track.CompletionStatus != CompletionStatus.FileMissing);

    private static void Paragraph(StringBuilder document, string formatting, string text)
    {
        document.Append(formatting);
        AppendEscaped(document, text);
        document.Append(@"\par").Append('\n');
    }

    /// <summary>Writes text that a reader is meant to see rather than a reader's parser.</summary>
    /// <remarks>
    /// RTF is seven-bit, so an accent in a title goes in as its code point and the <c>?</c> after
    /// it is the character a reader too old to know the escape shows instead. A control character
    /// has no plain-text spelling at all, so tabs and line breaks become a space and the rest of
    /// them are left out.
    /// </remarks>
    private static void AppendEscaped(StringBuilder document, string text)
    {
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                case '{':
                case '}':
                    document.Append('\\').Append(character);
                    break;
                case '\t':
                case '\r':
                case '\n':
                    document.Append(' ');
                    break;
                default:
                    if (character >= 128)
                    {
                        document.Append(@"\u")
                            .Append(((short)character).ToString(CultureInfo.InvariantCulture))
                            .Append('?');
                    }
                    else if (!char.IsControl(character))
                    {
                        // A tag written by something that got its bytes wrong carries the odd
                        // control character, and RTF has no plain-text spelling for one, so
                        // anything left down there is left out of the document entirely.
                        document.Append(character);
                    }

                    break;
            }
        }
    }
}
