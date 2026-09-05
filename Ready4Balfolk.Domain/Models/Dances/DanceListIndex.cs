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

    private DanceListIndex(
        Dictionary<string, string> slugByFoldedName, Dictionary<string, Dance> danceBySlug, DanceWords words)
    {
        _slugByFoldedName = slugByFoldedName;
        _danceBySlug = danceBySlug;
        Words = words;

        // Longest first, so scanning a filename for any known name prefers "Bourrée 3 temps" over
        // the "Bourrée" sitting inside it.
        FoldedNamesLongestFirst = [.. slugByFoldedName.Keys.OrderByDescending(n => n.Length).ThenBy(n => n, StringComparer.Ordinal)];
    }

    public static DanceListIndex Empty { get; } = Build(DanceList.Empty);

    /// <summary>The word lists the file ships, and the match key they produce.</summary>
    public DanceWords Words { get; }

    /// <summary>Every known name as a match key, longest first.</summary>
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
        var words = DanceWords.From(list);

        foreach (var dance in list.Dances)
        {
            if (!danceBySlug.TryAdd(dance.Slug, dance))
            {
                continue;
            }

            foreach (var name in dance.Names)
            {
                // Keyed rather than merely folded, so the five ways of writing one bourrée are one
                // entry and a file spelling it any of them lands on the dance.
                var key = words.KeyFor(name);
                if (key.Length > 0)
                {
                    slugByFoldedName.TryAdd(key, dance.Slug);
                }
            }
        }

        return new DanceListIndex(slugByFoldedName, danceBySlug, words);
    }

    /// <summary>The slug the given name belongs to, or null when the list does not know it.</summary>
    public string? ResolveSlug(string name)
    {
        var key = Words.KeyFor(name);
        return key.Length == 0 ? null : _slugByFoldedName.GetValueOrDefault(key);
    }

    public Dance? FindBySlug(string slug) => _danceBySlug.GetValueOrDefault(slug);

    /// <summary>
    /// What an approval of this text holds: the slug when the list knows the value, the text itself
    /// when it does not.
    /// </summary>
    /// <remarks>
    /// The slug is the identity and the names on it are a flat set of equals the published list is
    /// free to re-spell, so an answer written down as a name silently re-points to another dance the
    /// day that name moves, and vanishes the day it is dropped. Text is kept only for a value the
    /// list has never heard of: that is what parks a track, and keeping it is what lets a later
    /// import release it without anybody being asked twice.
    /// </remarks>
    public string ApprovedValueFor(string value) => ResolveSlug(value) ?? value;

    /// <summary>The name to show for a slug, or the slug itself when the list no longer has it.</summary>
    public string DisplayNameFor(string slug) => FindBySlug(slug)?.DisplayName ?? slug;
}
