using System.Text;
using System.Text.RegularExpressions;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>What is wrong with a pattern, or nothing.</summary>
/// <remarks>
/// A pattern is refused rather than half-understood. A user writing a rule is taking responsibility
/// for what it does to their whole library, and a rule that quietly means something other than what
/// it looks like is the opposite of the bargain.
/// </remarks>
public enum PatternProblem
{
    None,

    Empty,

    /// <summary>A `%` followed by something that is not a token.</summary>
    UnknownToken,

    /// <summary>Nothing but literal text: it would match one file name and nothing else.</summary>
    NoFields,

    /// <summary>Two tokens with nothing between them, so where one ends is anybody's guess.</summary>
    AdjacentFields,

    /// <summary>The same field twice, which cannot both be true.</summary>
    DuplicateField
}

/// <summary>One field a pattern picked out of a file name.</summary>
public sealed record FileNamePatternMatch
{
    public string? Dance { get; init; }

    public string? Artist { get; init; }

    public string? Title { get; init; }

    public string? TrackNumber { get; init; }
}

/// <summary>
/// A user's statement that their file names are shaped a certain way, compiled so it can be run
/// over a library.
/// </summary>
/// <remarks>
/// <para>
/// Tokens are <c>%d</c> dance, <c>%a</c> artist, <c>%t</c> title, <c>%n</c> track number,
/// <c>%i</c> ignore and <c>%ex</c> extension. Everything else is literal text that has to be there.
/// </para>
/// <para>
/// A pattern matches whole or not at all, and each field stops at the next piece of literal text,
/// with the last one taking whatever is left. That is the only reading that makes
/// <c>%a - %t</c> mean what a person expects on "Bal O'Gadjo - Le badaud - Live".
/// </para>
/// </remarks>
public sealed class FileNamePattern
{
    /// <summary>Long enough for any real file name, short enough that a bad pattern cannot hang a scan.</summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly Regex _regex;

    private FileNamePattern(string text, Regex regex, bool usesExtension)
    {
        Text = text;
        _regex = regex;
        UsesExtension = usesExtension;
    }

    /// <summary>The pattern as the user wrote it.</summary>
    public string Text { get; }

    /// <summary>Whether it is matched against the name with its extension on.</summary>
    public bool UsesExtension { get; }

    /// <summary>Compiles a pattern, or says why it will not do.</summary>
    public static (FileNamePattern? Pattern, PatternProblem Problem) Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, PatternProblem.Empty);
        }

        var expression = new StringBuilder("^");
        var seen = new List<TokenKind>();
        var literalSinceLastToken = true;
        var ignoreCount = 0;
        var index = 0;

        while (index < text.Length)
        {
            if (text[index] != '%')
            {
                expression.Append(Regex.Escape(text[index].ToString()));
                literalSinceLastToken = true;
                index++;
                continue;
            }

            var token = ReadToken(text, index);
            if (token is null)
            {
                return (null, PatternProblem.UnknownToken);
            }

            var (kind, length) = token.Value;

            if (!literalSinceLastToken)
            {
                return (null, PatternProblem.AdjacentFields);
            }

            if (kind is not TokenKind.Ignore && seen.Contains(kind))
            {
                return (null, PatternProblem.DuplicateField);
            }

            expression.Append(GroupFor(kind, ignoreCount));
            if (kind is TokenKind.Ignore)
            {
                ignoreCount++;
            }

            seen.Add(kind);
            literalSinceLastToken = false;
            index += length;
        }

        if (seen.Count == 0)
        {
            return (null, PatternProblem.NoFields);
        }

        if (seen.All(kind => kind is TokenKind.Ignore or TokenKind.Extension or TokenKind.TrackNumber))
        {
            // A pattern that captures no field of the track answers nothing, however well it matches.
            return (null, PatternProblem.NoFields);
        }

        // The last field takes the rest of the name rather than the least it can get away with, so
        // "%a - %t" leaves nothing of "Le badaud - Live" behind.
        expression.Append('$');
        var pattern = MakeLastGroupGreedy(expression.ToString());

        var regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);

        return (new FileNamePattern(text, regex, seen.Contains(TokenKind.Extension)), PatternProblem.None);
    }

    /// <summary>What this pattern makes of one file name, or null when it does not match it whole.</summary>
    /// <param name="fileName">
    /// The name with its extension when the pattern asked for one, without it otherwise, so a
    /// pattern that says nothing about extensions is not tripped by them.
    /// </param>
    public FileNamePatternMatch? Match(string fileName)
    {
        Match match;
        try
        {
            match = _regex.Match(fileName);
        }
        catch (RegexMatchTimeoutException)
        {
            // A pathological pattern against a pathological name. One file failing to match is a
            // normal outcome; taking the scan down with it is not.
            return null;
        }

        if (!match.Success)
        {
            return null;
        }

        return new FileNamePatternMatch
        {
            Dance = Captured(match, "d"),
            Artist = Captured(match, "a"),
            Title = Captured(match, "t"),
            TrackNumber = Captured(match, "n")
        };
    }

    private static string? Captured(Match match, string group)
    {
        var value = match.Groups[group];
        if (!value.Success)
        {
            return null;
        }

        var trimmed = value.Value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }

    private static (TokenKind Kind, int Length)? ReadToken(string text, int at)
    {
        if (text.AsSpan(at).StartsWith("%ex", StringComparison.Ordinal))
        {
            return (TokenKind.Extension, 3);
        }

        if (at + 1 >= text.Length)
        {
            return null;
        }

        return text[at + 1] switch
        {
            'd' => (TokenKind.Dance, 2),
            'a' => (TokenKind.Artist, 2),
            't' => (TokenKind.Title, 2),
            'n' => (TokenKind.TrackNumber, 2),
            'i' => (TokenKind.Ignore, 2),
            _ => null
        };
    }

    private static string GroupFor(TokenKind kind, int ignoreCount) => kind switch
    {
        TokenKind.Dance => "(?<d>.+?)",
        TokenKind.Artist => "(?<a>.+?)",
        TokenKind.Title => "(?<t>.+?)",
        // Digits only. A track number that is not a number is not a track number, and a pattern
        // claiming otherwise should fail the file rather than swallow it.
        TokenKind.TrackNumber => @"(?<n>\d+)",
        TokenKind.Extension => "(?<ex>[^.]+)",
        _ => $"(?<i{ignoreCount}>.+?)"
    };

    /// <summary>Makes the final field greedy, so it keeps the tail of the name instead of dropping it.</summary>
    private static string MakeLastGroupGreedy(string expression)
    {
        var at = expression.LastIndexOf(".+?)", StringComparison.Ordinal);
        return at < 0 ? expression : string.Concat(expression.AsSpan(0, at), ".+)", expression.AsSpan(at + 4));
    }

    private enum TokenKind
    {
        Dance,
        Artist,
        Title,
        TrackNumber,
        Ignore,
        Extension
    }
}
