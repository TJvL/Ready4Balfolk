using System.Globalization;
using System.Text;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.History;

/// <summary>The same evening, for something that imports it rather than reads it.</summary>
/// <remarks>
/// <para>
/// The report is for a person, and a rights organisation that takes an upload or hands out a
/// template wants rows instead. The same three columns either way, so whichever they asked for is
/// the same evening.
/// </para>
/// <para>
/// CSV rather than a spreadsheet file, because a real one is a dependency and a zip container to
/// answer a question a comma answers.
/// </para>
/// </remarks>
public static class NightSpreadsheet
{
    /// <summary>The night as CSV, one track per row under a header.</summary>
    public static string Render(QueueHistory night)
    {
        var rows = new StringBuilder();

        Row(rows,
            DomainStrings.NightReport_TimeColumn,
            DomainStrings.NightReport_ArtistColumn,
            DomainStrings.NightReport_TitleColumn);

        foreach (var track in NightReport.Played(night))
        {
            Row(rows,
                track.StartedAt?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? string.Empty,
                track.Artist,
                track.Title);
        }

        return rows.ToString();
    }

    /// <remarks>
    /// CRLF, which is what RFC 4180 says a record ends with and what the spreadsheet on the other
    /// end is least surprised by.
    /// </remarks>
    private static void Row(StringBuilder rows, string time, string artist, string title)
    {
        AppendField(rows, time);
        rows.Append(',');
        AppendField(rows, artist);
        rows.Append(',');
        AppendField(rows, title);
        rows.Append("\r\n");
    }

    /// <summary>Writes one field so that what comes back out is what went in.</summary>
    /// <remarks>
    /// <para>
    /// A control character has no spelling here, so tabs and line breaks become a space and the
    /// rest are left out, the same as the report does. What is left is quoted whenever it holds a
    /// comma or a quote, with the quotes inside it doubled: a band called <c>Naragonia, Duo</c> is
    /// otherwise two columns and every row after it is read one column across.
    /// </para>
    /// <para>
    /// A field that starts with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> is a formula to Excel and
    /// LibreOffice, so an artist written <c>-M-</c> comes back as an error, a title <c>+1</c> as a
    /// number and a tag reading <c>=HYPERLINK(...)</c> as a live link in the organiser's sheet. It
    /// goes out behind a <c>'</c>, which is how a spreadsheet spells "this is text", and quoted.
    /// </para>
    /// </remarks>
    private static void AppendField(StringBuilder rows, string value)
    {
        var cleaned = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\t' or '\r' or '\n')
            {
                cleaned.Append(' ');
            }
            else if (!char.IsControl(character))
            {
                cleaned.Append(character);
            }
        }

        var field = cleaned.ToString();
        var formula = field is ['=' or '+' or '-' or '@', ..];
        if (formula)
        {
            field = "'" + field;
        }

        if (!formula && !field.Contains(',', StringComparison.Ordinal) && !field.Contains('"', StringComparison.Ordinal))
        {
            rows.Append(field);
            return;
        }

        rows.Append('"').Append(field.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
    }
}
