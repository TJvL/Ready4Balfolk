using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Finds names from the dance list anywhere in a piece of text.</summary>
/// <remarks>
/// <para>
/// This is the whole reason a dance list exists. A filename cannot be split on a separator and
/// trusted, because real libraries put the dance in brackets after the title, at the end after a
/// dash, or nowhere at all. Scanning for names the user has actually declared answers with a dance
/// or with nothing, and never with a band name.
/// </para>
/// <para>
/// Matching is on whole words, not on substrings: "Andro" must not match inside "Androgyne", and
/// "Valse" must not match inside "Valsette".
/// </para>
/// </remarks>
public static class DanceNameScanner
{
    /// <summary>
    /// Every dance whose name appears in the text, longest name first so that "Bourrée 3 temps"
    /// wins over the "Bourrée" sitting inside it.
    /// </summary>
    public static IReadOnlyList<(string Slug, string MatchedName)> Scan(string? text, DanceListIndex index)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var haystack = StringNormalizer.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (haystack.Length == 0)
        {
            return [];
        }

        var found = new List<(string Slug, string MatchedName)>();
        var claimed = new bool[haystack.Length];

        // FoldedNamesLongestFirst is already ordered, so the first name to claim a run of words is
        // the most specific one that fits there.
        foreach (var folded in index.FoldedNamesLongestFirst)
        {
            var needle = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (needle.Length == 0 || needle.Length > haystack.Length)
            {
                continue;
            }

            var at = IndexOfUnclaimed(haystack, needle, claimed);
            if (at < 0)
            {
                continue;
            }

            var slug = index.ResolveSlug(folded);
            if (slug is null)
            {
                continue;
            }

            for (var i = at; i < at + needle.Length; i++)
            {
                claimed[i] = true;
            }

            found.Add((slug, folded));
        }

        return found;
    }

    private static int IndexOfUnclaimed(string[] haystack, string[] needle, bool[] claimed)
    {
        for (var start = 0; start + needle.Length <= haystack.Length; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (claimed[start + offset]
                    || !string.Equals(haystack[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return start;
            }
        }

        return -1;
    }
}
