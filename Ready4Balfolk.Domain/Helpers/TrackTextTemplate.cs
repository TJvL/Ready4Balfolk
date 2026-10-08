using System.Text;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Helpers;

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
/// <para>
/// Brackets around a field are part of that field rather than separators, so they go with it as a
/// pair: <c>%t (%d)</c> on a track with no dance is the title, not the title and a closing bracket
/// with nothing to close.
/// </para>
/// </remarks>
public static class TrackTextTemplate
{
    private static readonly char[] Openers = ['(', '[', '{'];

    private static readonly char[] Closers = [')', ']', '}'];

    private static readonly char[] Brackets = [.. Openers, .. Closers];

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

        var segments = Pair(Parse(template));
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
            text.Append(segment.Opens).Append(value).Append(segment.Closes);
            said = true;
        }

        return said ? Trailing(segments, text).ToString().Trim() : string.Empty;
    }

    /// <summary>
    /// The literal in front of a field, dropped when it would open the line with a separator.
    /// </summary>
    /// <remarks>
    /// What the template itself opens with is kept, because that is somebody writing "Now: %t"
    /// rather than a separator left over from a field that had nothing in it.
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

    /// <summary>
    /// Hands each field the brackets around it, taken out of the literals on either side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A literal is otherwise attributed to the field after it, which is right for a separator and
    /// wrong for a bracket: the opening half went with the field and the closing half stayed behind
    /// as what the template ends with, so a track with no dance read "Salamandre)".
    /// </para>
    /// <para>
    /// Only a bracket that is closed by its own partner is a pair. Text inside it goes with it, so
    /// <c>(by %a)</c> is dropped whole, and one that never closes is left where it was written, a
    /// literal like any other, rather than guessed at.
    /// </para>
    /// </remarks>
    private static List<Segment> Pair(List<Segment> segments)
    {
        for (var index = 1; index + 1 < segments.Count; index++)
        {
            if (segments[index].Field is null
                || segments[index - 1].Field is not null
                || segments[index + 1].Field is not null)
            {
                continue;
            }

            var before = segments[index - 1].Text;
            var after = segments[index + 1].Text;
            var opens = string.Empty;
            var closes = string.Empty;

            // Inside out, so "[(%d)]" pairs the round ones first and then the square ones.
            while (Peel(ref before, ref after, out var opening, out var closing))
            {
                opens = opening + opens;
                closes += closing;
            }

            segments[index - 1] = segments[index - 1] with { Text = before };
            segments[index] = segments[index] with { Opens = opens, Closes = closes };
            segments[index + 1] = segments[index + 1] with { Text = after };
        }

        return segments;
    }

    /// <summary>
    /// Takes the innermost bracket the literal in front leaves open, and its partner from the
    /// literal behind, when the first bracket behind is that partner.
    /// </summary>
    private static bool Peel(ref string before, ref string after, out string opening, out string closing)
    {
        opening = closing = string.Empty;

        var open = before.LastIndexOfAny(Openers);
        if (open < 0 || before.IndexOfAny(Closers, open) >= 0)
        {
            return false;
        }

        var close = after.IndexOfAny(Brackets);
        if (close < 0 || after[close] != Closers[Array.IndexOf(Openers, before[open])])
        {
            return false;
        }

        opening = before[open..];
        closing = after[..(close + 1)];
        before = before[..open];
        after = after[(close + 1)..];
        return true;
    }

    private readonly record struct Segment(string Text, char? Field)
    {
        public string Opens { get; init; } = string.Empty;

        public string Closes { get; init; } = string.Empty;
    }
}
