using System.Text;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Presentation;

/// <summary>How a track is written on a screen, in the user's own words.</summary>
/// <remarks>
/// <para>
/// The same placeholders the file name patterns use, because an application with two vocabularies
/// for the same three fields is an application with two things to learn. They read the other way
/// round here: a pattern takes a name apart, a template puts one together.
/// </para>
/// <para>
/// A library always has a track missing one of them, so a field with nothing in it takes its
/// separator with it: <c>%a - %t</c> on a track with no title is the artist, not the artist and a
/// dangling dash.
/// </para>
/// </remarks>
public static class TrackTextTemplate
{
    /// <summary>The dance, the artist and the title, which is all a track carries to say.</summary>
    public const string Placeholders = "%d %a %t";

    /// <summary>Writes the track the way the template says, or nothing when it says nothing.</summary>
    public static string Render(string? template, Track? track)
    {
        return track is null || string.IsNullOrWhiteSpace(template)
            ? string.Empty
            : Render(template, track.Dance, track.Artist, track.Title);
    }

    /// <summary>The same, for what history holds, which is fields rather than a track.</summary>
    public static string Render(string? template, string dance, string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var segments = Parse(template);
        var said = false;
        var text = new StringBuilder();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (segment.Field is null)
            {
                // A literal is held back until the field it introduces turns out to have something
                // in it, which is what keeps the separators of empty fields off the screen.
                continue;
            }

            var value = segment.Field switch
            {
                'd' => dance,
                'a' => artist,
                _ => title
            };

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            text.Append(Between(segments, index, said));
            text.Append(value);
            said = true;
        }

        return said ? Trailing(segments, text).ToString().Trim() : string.Empty;
    }

    /// <summary>
    /// The literal in front of a field, dropped when it would open the line with a separator.
    /// </summary>
    /// <remarks>
    /// What the template itself opens with is kept, because that is somebody writing "(%d)" rather
    /// than a separator left over from a field that had nothing in it.
    /// </remarks>
    private static string Between(IReadOnlyList<Segment> segments, int index, bool anythingYet)
    {
        var opens = index == 1;
        return index > 0 && segments[index - 1].Field is null && (anythingYet || opens)
            ? segments[index - 1].Text
            : string.Empty;
    }

    /// <summary>Whatever the template ends with, kept only when a field came before it.</summary>
    private static StringBuilder Trailing(IReadOnlyList<Segment> segments, StringBuilder text) =>
        segments.Count > 0 && segments[^1].Field is null && segments[^1].Text.Trim().Length > 0
            ? text.Append(segments[^1].Text)
            : text;

    private static List<Segment> Parse(string template)
    {
        var segments = new List<Segment>();
        var literal = new StringBuilder();

        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '%' || index + 1 >= template.Length)
            {
                literal.Append(template[index]);
                continue;
            }

            var next = template[index + 1];

            // %% is a per cent sign somebody meant, which a template full of per cent signs needs.
            if (next == '%')
            {
                literal.Append('%');
                index++;
                continue;
            }

            if (next is not ('d' or 'a' or 't'))
            {
                // Left as it was written, so a placeholder nobody has heard of is visible on screen
                // rather than quietly swallowed.
                literal.Append(template[index]);
                continue;
            }

            segments.Add(new Segment(literal.ToString(), null));
            literal.Clear();
            segments.Add(new Segment(string.Empty, next));
            index++;
        }

        if (literal.Length > 0)
        {
            segments.Add(new Segment(literal.ToString(), null));
        }

        return segments;
    }

    private readonly record struct Segment(string Text, char? Field);
}
