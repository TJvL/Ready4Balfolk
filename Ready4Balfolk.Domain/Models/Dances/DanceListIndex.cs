using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>A lookup built over a <see cref="DanceList"/>: folded name to slug, slug to dance.</summary>
/// <remarks>
/// Resolution answers a slug or nothing. A name belongs to exactly one dance, which is the property
/// that lets discovery answer with one dance rather than a set, so the index is built once and
/// rebuilt whole whenever the list changes rather than being patched.
/// </remarks>
public sealed class DanceListIndex
{
    private readonly Dictionary<string, string> _slugByFoldedName;
    private readonly Dictionary<string, Dance> _danceBySlug;

    private DanceListIndex(Dictionary<string, string> slugByFoldedName, Dictionary<string, Dance> danceBySlug)
    {
        _slugByFoldedName = slugByFoldedName;
        _danceBySlug = danceBySlug;

        // Longest first, so scanning a filename for any known name prefers "Bourrée 3 temps" over
        // the "Bourrée" sitting inside it.
        FoldedNamesLongestFirst = [.. slugByFoldedName.Keys.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal)];
    }

    public static DanceListIndex Empty { get; } = Build(DanceList.Empty);

    /// <summary>Every known name, folded, longest first.</summary>
    public IReadOnlyList<string> FoldedNamesLongestFirst { get; }

    public IReadOnlyCollection<Dance> Dances => _danceBySlug.Values;

    /// <summary>
    /// Builds an index. A name that is already taken is skipped rather than overwriting the first
    /// claim: the list screen and the importer both refuse duplicates, so one reaching here means
    /// a hand-edited file, and silently reassigning names would be a worse answer than ignoring one.
    /// </summary>
    public static DanceListIndex Build(DanceList list)
    {
        var slugByFoldedName = new Dictionary<string, string>(StringComparer.Ordinal);
        var danceBySlug = new Dictionary<string, Dance>(StringComparer.Ordinal);

        foreach (var dance in list.Dances)
        {
            if (!danceBySlug.TryAdd(dance.Slug, dance))
            {
                continue;
            }

            foreach (var name in dance.Names)
            {
                var folded = StringNormalizer.Normalize(name);
                if (folded.Length > 0)
                {
                    slugByFoldedName.TryAdd(folded, dance.Slug);
                }
            }
        }

        return new DanceListIndex(slugByFoldedName, danceBySlug);
    }

    /// <summary>The slug the given name belongs to, or null when the list does not know it.</summary>
    public string? ResolveSlug(string name)
    {
        var folded = StringNormalizer.Normalize(name);
        return folded.Length == 0 ? null : _slugByFoldedName.GetValueOrDefault(folded);
    }

    public Dance? FindBySlug(string slug) => _danceBySlug.GetValueOrDefault(slug);

    /// <summary>The name to show for a slug, or the slug itself when the list no longer has it.</summary>
    public string DisplayNameFor(string slug) => FindBySlug(slug)?.DisplayName ?? slug;
}
