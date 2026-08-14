using System;
using System.Collections.Generic;
using System.Linq;
using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>
/// The one matching and walking logic behind every dance picker, on the review rows and in the
/// edit dialog alike, so the two can never drift apart on what typing "bou" offers.
/// </summary>
public static class DancePicking
{
    /// <summary>Enough to see, few enough to walk.</summary>
    private const int MostMatches = 12;

    /// <summary>What the list has to offer for the text as it stands, first one highlighted.</summary>
    /// <remarks>
    /// Starting with what was typed first, then merely containing it: somebody typing "bou" means
    /// bourrée, and the dances that only mention it belong underneath.
    /// </remarks>
    public static (IReadOnlyList<DanceMatch> Matches, bool Open) MatchesFor(
        IReadOnlyList<string> allDances, string typedRaw)
    {
        var typed = StringNormalizer.Normalize(typedRaw);
        if (typed.Length == 0)
        {
            return ([], false);
        }

        var names = allDances
            .Select(name => (Name: name, Folded: StringNormalizer.Normalize(name)))
            .Where(candidate => candidate.Folded.Contains(typed, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Folded.StartsWith(typed, StringComparison.Ordinal))
            .ThenBy(candidate => candidate.Name, StringComparer.CurrentCulture)
            .Select(candidate => candidate.Name)
            .Take(MostMatches)
            .ToList();

        // Nothing to choose between when the only match is what is already written.
        var open = names.Count > 0
            && !(names.Count == 1 && string.Equals(StringNormalizer.Normalize(names[0]), typed, StringComparison.Ordinal));

        return ([.. names.Select((name, index) => new DanceMatch(name) { IsHighlighted = index == 0 })], open);
    }

    /// <summary>Walks the offered names, wrapping, which is the whole reason the list is ours.</summary>
    public static void MoveHighlight(IReadOnlyList<DanceMatch> matches, int direction)
    {
        if (matches.Count == 0)
        {
            return;
        }

        var at = matches.ToList().FindIndex(match => match.IsHighlighted);
        var next = (at + direction + matches.Count) % matches.Count;

        for (var i = 0; i < matches.Count; i++)
        {
            matches[i].IsHighlighted = i == next;
        }
    }
}
