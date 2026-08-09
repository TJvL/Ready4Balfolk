using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>Works out what can be done about a value the dance list does not know.</summary>
public static class UnrecognisedValueClassifier
{
    /// <summary>
    /// How far a value may be from a name and still be called a misspelling.
    /// </summary>
    /// <remarks>
    /// Scaled with length rather than fixed: one edit is a lot in "Andro" and very little in
    /// "Cercle Circassien".
    /// </remarks>
    private static int AllowedDistanceFor(string folded) => folded.Length switch
    {
        <= 4 => 1,
        <= 8 => 2,
        _ => 3
    };

    /// <summary>Decides what kind of unrecognised value this is, and what it might mean.</summary>
    public static (UnrecognisedKind Kind, IReadOnlyList<string> Slugs) Classify(string value, DanceListIndex index)
    {
        var folded = StringNormalizer.Normalize(value);
        if (folded.Length == 0)
        {
            return (UnrecognisedKind.Unknown, []);
        }

        // The list already knows this name. Nothing is misspelled and nothing needs mapping: these
        // tracks named this dance and at least one other, and discovery declined to choose between
        // them. Calling it a misspelling of itself is how this used to read.
        if (index.ResolveSlug(folded) is { } known)
        {
            return (UnrecognisedKind.Ambiguous, [known]);
        }

        // Too general first, because a value that sits inside several names would also be within
        // edit distance of them, and calling it a misspelling is exactly the wrong answer.
        var containing = index.FoldedNamesLongestFirst
            .Where(name => ContainsAsWords(name, folded))
            .Select(index.ResolveSlug)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (containing.Count > 1)
        {
            return (UnrecognisedKind.TooGeneral, containing);
        }

        if (containing.Count == 1)
        {
            // Inside exactly one name: "Bourrée Auvergnate" when only one bourrée is listed. That is
            // a single decision after all.
            return (UnrecognisedKind.Misspelled, containing);
        }

        var limit = AllowedDistanceFor(folded);
        var near = index.FoldedNamesLongestFirst
            .Select(name => (Name: name, Distance: EditDistance.Between(folded, name, limit)))
            .Where(candidate => candidate.Distance <= limit)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => index.ResolveSlug(candidate.Name))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return near.Count switch
        {
            0 => (UnrecognisedKind.Unknown, []),
            1 => (UnrecognisedKind.Misspelled, near),
            // Near several dances at once: offering to map it would be a guess between them.
            _ => (UnrecognisedKind.TooGeneral, near)
        };
    }

    /// <summary>
    /// Whether the needle appears in the name as whole words, so "Bourrée" is inside
    /// "Bourrée 3 temps" but "Ron" is not inside "Rond de Landéda".
    /// </summary>
    private static bool ContainsAsWords(string foldedName, string foldedNeedle)
    {
        if (string.Equals(foldedName, foldedNeedle, StringComparison.Ordinal))
        {
            return false;
        }

        var name = foldedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var needle = foldedNeedle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (needle.Length == 0 || needle.Length > name.Length)
        {
            return false;
        }

        for (var start = 0; start + needle.Length <= name.Length; start++)
        {
            var matches = true;
            for (var offset = 0; offset < needle.Length; offset++)
            {
                if (!string.Equals(name[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
