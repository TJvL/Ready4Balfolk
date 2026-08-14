using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>Checks the invariants the rest of the feature is built on.</summary>
/// <remarks>
/// BigBalfolkList's own build enforces all of this, so a file that fails here has been edited by
/// hand or truncated in transit. The one that matters most is that a name means one dance: it is
/// what lets discovery answer with a dance rather than a set.
/// </remarks>
public static class DanceListValidation
{
    /// <summary>Finds every problem in a list, rather than stopping at the first.</summary>
    public static DanceListProblems Validate(DanceList list)
    {
        var duplicateNames = new List<string>();
        var duplicateSlugs = new List<string>();
        var slugsWithoutNames = new List<string>();
        var undeclaredTags = new List<string>();

        var ownerByFoldedName = new Dictionary<string, string>(StringComparer.Ordinal);
        var words = DanceWords.From(list);
        var seenSlugs = new HashSet<string>(StringComparer.Ordinal);
        var declaredTags = new HashSet<string>(list.Tags, StringComparer.Ordinal);

        foreach (var dance in list.Dances)
        {
            if (!seenSlugs.Add(dance.Slug))
            {
                duplicateSlugs.Add(dance.Slug);
            }

            foreach (var tag in dance.Tags.Where(tag => !declaredTags.Contains(tag)))
            {
                undeclaredTags.Add(tag);
            }

            var usableNames = dance.Names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (usableNames.Count == 0)
            {
                slugsWithoutNames.Add(dance.Slug);
                continue;
            }

            foreach (var name in usableNames)
            {
                // The key, not the folded name: "Bourrée 3 temps" and "Bourrée à trois temps" are
                // one name, and two dances carrying them would be one name meaning two dances.
                var folded = words.KeyFor(name);
                if (folded.Length == 0)
                {
                    continue;
                }

                // Compare within the dance too: two spellings that fold together are the same
                // string as far as anything downstream is concerned.
                if (ownerByFoldedName.TryGetValue(folded, out var owner)
                    && !string.Equals(owner, dance.Slug, StringComparison.Ordinal))
                {
                    duplicateNames.Add(name);
                }
                else
                {
                    ownerByFoldedName[folded] = dance.Slug;
                }
            }
        }

        return duplicateNames.Count == 0
               && duplicateSlugs.Count == 0
               && slugsWithoutNames.Count == 0
               && undeclaredTags.Count == 0
            ? DanceListProblems.None
            : new DanceListProblems(duplicateNames, duplicateSlugs, slugsWithoutNames, undeclaredTags);
    }
}
