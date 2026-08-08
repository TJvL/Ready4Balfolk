using System.Text;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Editor;

/// <summary>Pure transformations of a dance list.</summary>
/// <remarks>
/// Categories are addressed by their position, because that is what a tree gives you. Dances are
/// addressed by their slug, because that is their identity: a dance keeps its slug when it is
/// renamed, respelled or moved, so an edit cannot land on the wrong one.
/// </remarks>
public static class DanceListTransforms
{
    public static DanceList AddCategory(DanceList list, int[] parentPath, string? name = null)
    {
        var category = new DanceCategory
        {
            Name = name ?? GenerateUniqueCategoryName(list, parentPath),
            Weight = 1
        };

        return parentPath.Length == 0
            ? list with { Categories = [.. list.Categories, category] }
            : list with
            {
                Categories = ReplaceAt(list.Categories, parentPath, 0,
                    parent => parent with { Categories = [.. parent.Categories, category] })
            };
    }

    public static DanceList RenameCategory(DanceList list, int[] path, string newName) =>
        list with { Categories = ReplaceAt(list.Categories, path, 0, c => c with { Name = newName.Trim() }) };

    public static DanceList ReweightCategory(DanceList list, int[] path, int newWeight) =>
        list with { Categories = ReplaceAt(list.Categories, path, 0, c => c with { Weight = newWeight }) };

    public static DanceList DeleteCategory(DanceList list, int[] path)
    {
        if (path.Length == 0)
        {
            return list;
        }

        if (path.Length == 1)
        {
            return list with { Categories = [.. list.Categories.Where((_, i) => i != path[0])] };
        }

        var childIndex = path[^1];
        return list with
        {
            Categories = ReplaceAt(list.Categories, path[..^1], 0,
                parent => parent with { Categories = [.. parent.Categories.Where((_, i) => i != childIndex)] })
        };
    }

    public static DanceList AddDance(DanceList list, int[] categoryPath, string name)
    {
        if (categoryPath.Length == 0)
        {
            // Every dance lives in a category, so that randomisation always has a weight to apply.
            return list;
        }

        var dance = new Dance
        {
            Slug = GenerateUniqueSlug(list, name),
            Names = [name.Trim()],
            Weight = 1
        };

        return list with
        {
            Categories = ReplaceAt(list.Categories, categoryPath, 0,
                category => category with { Dances = [.. category.Dances, dance] })
        };
    }

    public static DanceList DeleteDance(DanceList list, string slug) =>
        list with
        {
            Categories = MapCategories(list.Categories, category => category with
            {
                Dances = [.. category.Dances.Where(d => !string.Equals(d.Slug, slug, StringComparison.Ordinal))]
            })
        };

    /// <summary>Moves a dance to another category, keeping its slug, names and weight.</summary>
    public static DanceList MoveDance(DanceList list, string slug, int[] targetCategoryPath)
    {
        var dance = list.AllDances.FirstOrDefault(d => string.Equals(d.Slug, slug, StringComparison.Ordinal));
        if (dance is null || targetCategoryPath.Length == 0)
        {
            return list;
        }

        var without = DeleteDance(list, slug);
        return without with
        {
            Categories = ReplaceAt(without.Categories, targetCategoryPath, 0,
                category => category with { Dances = [.. category.Dances, dance] })
        };
    }

    public static DanceList ReweightDance(DanceList list, string slug, int newWeight) =>
        MapDance(list, slug, dance => dance with { Weight = newWeight });

    public static DanceList AddName(DanceList list, string slug, string name) =>
        MapDance(list, slug, dance => dance with { Names = [.. dance.Names, name.Trim()] });

    public static DanceList RemoveNameAt(DanceList list, string slug, int index) =>
        MapDance(list, slug, dance => index < 0 || index >= dance.Names.Count || dance.Names.Count == 1
            // A dance with no names could never be read or matched again, so the last one stays.
            ? dance
            : dance with { Names = [.. dance.Names.Where((_, i) => i != index)] });

    /// <summary>
    /// Moves a name within its dance. Moving one to the front is how a user chooses the spelling
    /// they read, and it moves nothing else, because everything refers to the slug.
    /// </summary>
    public static DanceList MoveName(DanceList list, string slug, int fromIndex, int toIndex) =>
        MapDance(list, slug, dance =>
        {
            var names = dance.Names.ToList();
            if (fromIndex < 0 || fromIndex >= names.Count || toIndex < 0 || toIndex >= names.Count
                || fromIndex == toIndex)
            {
                return dance;
            }

            var name = names[fromIndex];
            names.RemoveAt(fromIndex);
            names.Insert(toIndex, name);
            return dance with { Names = names };
        });

    /// <summary>Whether a sibling category already answers to this name.</summary>
    public static bool IsCategoryNameFree(DanceList list, int[] parentPath, string name, int? excludeIndex = null)
    {
        var folded = StringNormalizer.Normalize(name);
        var siblings = ResolveSiblings(list, parentPath);
        for (var i = 0; i < siblings.Count; i++)
        {
            if (i != excludeIndex && StringNormalizer.Normalize(siblings[i].Name) == folded)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The dance a name already belongs to, or null when the name is free.</summary>
    public static string? FindNameOwner(DanceList list, string name, string? exceptSlug = null)
    {
        var folded = StringNormalizer.Normalize(name);
        return folded.Length == 0
            ? null
            : list.AllDances
                .Where(dance => !string.Equals(dance.Slug, exceptSlug, StringComparison.Ordinal))
                .FirstOrDefault(dance => dance.Names.Any(n => StringNormalizer.Normalize(n) == folded))
                ?.Slug;
    }

    /// <summary>The categories at a path's level, so a sibling check has something to look at.</summary>
    public static IReadOnlyList<DanceCategory> ResolveSiblings(DanceList list, int[] parentPath)
    {
        var level = list.Categories;
        foreach (var index in parentPath)
        {
            if (index < 0 || index >= level.Count)
            {
                return [];
            }

            level = level[index].Categories;
        }

        return level;
    }

    public static DanceCategory? ResolveCategory(DanceList list, int[] path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        var level = list.Categories;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] < 0 || path[i] >= level.Count)
            {
                return null;
            }

            if (i == path.Length - 1)
            {
                return level[path[i]];
            }

            level = level[path[i]].Categories;
        }

        return null;
    }

    /// <summary>Builds a slug from a name, in the shape BigBalfolkList uses, and makes it unique.</summary>
    public static string GenerateUniqueSlug(DanceList list, string name)
    {
        var baseSlug = Slugify(name);
        if (baseSlug.Length == 0)
        {
            baseSlug = "dance";
        }

        var taken = list.AllDances.Select(d => d.Slug).ToHashSet(StringComparer.Ordinal);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        var suffix = 2;
        while (taken.Contains($"{baseSlug}-{suffix}"))
        {
            suffix++;
        }

        return $"{baseSlug}-{suffix}";
    }

    private static string Slugify(string name)
    {
        var folded = StringNormalizer.Normalize(name);
        var builder = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            builder.Append(c == ' ' ? '-' : c);
        }

        return builder.ToString();
    }

    private static string GenerateUniqueCategoryName(DanceList list, int[] parentPath)
    {
        var baseName = DomainStrings.DanceListTransforms_NewCategory;
        if (IsCategoryNameFree(list, parentPath, baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (!IsCategoryNameFree(list, parentPath, $"{baseName} {suffix}"))
        {
            suffix++;
        }

        return $"{baseName} {suffix}";
    }

    private static DanceList MapDance(DanceList list, string slug, Func<Dance, Dance> transform) =>
        list with
        {
            Categories = MapCategories(list.Categories, category => category with
            {
                Dances =
                [
                    .. category.Dances.Select(dance =>
                        string.Equals(dance.Slug, slug, StringComparison.Ordinal) ? transform(dance) : dance)
                ]
            })
        };

    private static IReadOnlyList<DanceCategory> MapCategories(
        IReadOnlyList<DanceCategory> categories, Func<DanceCategory, DanceCategory> transform) =>
    [
        .. categories.Select(category => transform(category with
        {
            Categories = MapCategories(category.Categories, transform)
        }))
    ];

    private static IReadOnlyList<DanceCategory> ReplaceAt(
        IReadOnlyList<DanceCategory> siblings, int[] path, int depth, Func<DanceCategory, DanceCategory> transform)
    {
        var index = path[depth];
        if (index < 0 || index >= siblings.Count)
        {
            return siblings;
        }

        var result = siblings.ToArray();
        result[index] = depth == path.Length - 1
            ? transform(siblings[index])
            : siblings[index] with
            {
                Categories = ReplaceAt(siblings[index].Categories, path, depth + 1, transform)
            };

        return result;
    }
}
