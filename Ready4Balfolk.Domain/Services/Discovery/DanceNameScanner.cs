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
/// <para>
/// The text is read the way the list compares its own names: number words become their digits and
/// glue is stepped over, so "Bourrée à trois temps" in a file name finds the dance whose key is
/// "bourree 3". The cost is that glue no longer separates: "Valse de la mazurka" reads as
/// "valse mazurka", so a dance actually called that would match it. That is the same trade the
/// list makes on its own names, and it buys every file written in French, Dutch or German.
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

        var folded = StringNormalizer.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (folded.Length == 0)
        {
            return [];
        }

        var words = index.Words;
        var haystack = folded.Select(words.AsNumber).ToArray();
        var glue = haystack.Select(words.IsGlue).ToArray();

        var found = new List<(string Slug, string MatchedName)>();
        var claimed = new bool[haystack.Length];

        // FoldedNamesLongestFirst is already ordered, so the first name to claim a run of words is
        // the most specific one that fits there.
        foreach (var key in index.FoldedNamesLongestFirst)
        {
            var needle = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (needle.Length == 0 || needle.Length > haystack.Length)
            {
                continue;
            }

            var span = SpanOfUnclaimed(haystack, glue, needle, claimed);
            if (span is not var (at, length))
            {
                continue;
            }

            var slug = index.ResolveSlug(key);
            if (slug is null)
            {
                continue;
            }

            for (var i = at; i < at + length; i++)
            {
                claimed[i] = true;
            }

            found.Add((slug, key));
        }

        return found;
    }

    /// <summary>
    /// Where a name sits in the text, stepping over glue, or nothing.
    /// </summary>
    /// <remarks>
    /// The needle is a match key and carries no glue, so the text's glue has to be steppable or
    /// "valse a 3 temps" would never find "valse 3". A span may not start or end on glue: claiming
    /// the "de" on either side of a name would eat words the next name needs.
    /// </remarks>
    private static (int At, int Length)? SpanOfUnclaimed(
        string[] haystack, bool[] glue, string[] needle, bool[] claimed)
    {
        for (var start = 0; start < haystack.Length; start++)
        {
            if (claimed[start] || glue[start])
            {
                continue;
            }

            var at = start;
            var matched = 0;

            while (at < haystack.Length && matched < needle.Length)
            {
                if (claimed[at])
                {
                    break;
                }

                if (glue[at])
                {
                    at++;
                    continue;
                }

                if (!string.Equals(haystack[at], needle[matched], StringComparison.Ordinal))
                {
                    break;
                }

                matched++;
                at++;
            }

            if (matched == needle.Length)
            {
                return (start, at - start);
            }
        }

        return null;
    }
}
