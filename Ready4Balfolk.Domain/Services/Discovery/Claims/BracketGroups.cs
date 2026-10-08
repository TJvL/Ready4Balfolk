using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>What a file name says inside its brackets.</summary>
/// <remarks>
/// Brackets are how somebody writes a dance into a name on purpose, so they are the one thing that
/// breaks a tie between two dances a name mentions.
/// </remarks>
public static partial class BracketGroups
{
    /// <summary>The contents of every bracketed group in the name, as match keys.</summary>
    /// <remarks>
    /// Match keys rather than merely folded text, because the scanner's matched names are match
    /// keys too: glue dropped and number words as digits. Folded only, "(Rond de Saint-Vincent)"
    /// keeps its "de" while the matched name has lost it, and a dance written in brackets on
    /// purpose is never recognised as deliberate.
    /// </remarks>
    public static List<string> In(string fileName, DanceWords words) =>
    [
        .. AnyBrackets().Matches(fileName)
            .Select(match => words.KeyFor(match.Groups[1].Value))
            .Where(text => text.Length > 0)
    ];

    /// <summary>The contents of a trailing bracket, when it is not a year.</summary>
    public static string? Trailing(string fileName)
    {
        var match = BracketedText().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var inside = match.Groups[1].Value.Trim();

        // "(1997)" is an edition, not a dance. Nothing was claimed, so nothing is discarded.
        return inside.Length > 0 && !LooksLikeAYear(inside) ? inside : null;
    }

    [GeneratedRegex(@"[(\[]([^)\]]*)[)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex AnyBrackets();

    [GeneratedRegex(@"\(([^)]*)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedText();

    private static bool LooksLikeAYear(string value) => value.Length == 4 && value.All(char.IsDigit);
}
