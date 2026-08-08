using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>Checks the one invariant the rest of the feature is built on: a name means one dance.</summary>
public static class DanceListValidation
{
    /// <summary>Finds every problem in a list, rather than stopping at the first.</summary>
    /// <remarks>
    /// An importer refusing a file has to be able to say which names collided, or the user is sent
    /// to hunt through a hundred entries for a duplicate the application already found.
    /// </remarks>
    public static DanceListProblems Validate(DanceList list)
    {
        var duplicateNames = new List<string>();
        var duplicateSlugs = new List<string>();
        var slugsWithoutNames = new List<string>();

        var ownerByFoldedName = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenSlugs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dance in list.AllDances)
        {
            if (!seenSlugs.Add(dance.Slug))
            {
                duplicateSlugs.Add(dance.Slug);
            }

            var usableNames = dance.Names.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (usableNames.Count == 0)
            {
                slugsWithoutNames.Add(dance.Slug);
                continue;
            }

            foreach (var name in usableNames)
            {
                var folded = StringNormalizer.Normalize(name);
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

        var unnamedCategories = CollectUnnamedCategories(list.Categories, path: string.Empty);

        return duplicateNames.Count == 0
               && duplicateSlugs.Count == 0
               && slugsWithoutNames.Count == 0
               && unnamedCategories.Count == 0
            ? DanceListProblems.None
            : new DanceListProblems(duplicateNames, duplicateSlugs, slugsWithoutNames, unnamedCategories);
    }

    private static List<string> CollectUnnamedCategories(IReadOnlyList<DanceCategory> categories, string path)
    {
        var unnamed = new List<string>();
        for (var i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            var here = path.Length == 0 ? $"[{i}]" : $"{path}[{i}]";
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                unnamed.Add(here);
            }

            unnamed.AddRange(CollectUnnamedCategories(category.Categories, here));
        }

        return unnamed;
    }
}
