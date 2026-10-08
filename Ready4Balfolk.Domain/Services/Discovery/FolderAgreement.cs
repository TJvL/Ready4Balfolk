using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>
/// What the rest of a folder turned out to be, used to answer the files in it that named nothing.
/// </summary>
/// <remarks>
/// Extracted from TrackStore, where it was three private members tangled into the scan. It reads no
/// files and holds no state: everything it needs arrives as an argument, which is what lets it be
/// tested without a scan around it.
/// </remarks>
public static class FolderAgreement
{
    /// <summary>
    /// Gives a track the dance the rest of its folder turned out to be.
    /// </summary>
    /// <remarks>
    /// Only ever fills a gap. A folder in which every resolved track reads as one dance is real
    /// evidence about the ones that did not, whatever that folder happens to be, and it is the
    /// cheapest way to rescue a run of files that name the dance once and then stop.
    /// </remarks>
    public static string? AgreedDance(IReadOnlyCollection<string> resolvedSlugs)
    {
        if (resolvedSlugs.Count == 0)
        {
            return null;
        }

        // One dance, agreed by the folder. A folder holding several dances says nothing about the
        // track that named none of them.
        var distinct = resolvedSlugs.Distinct(StringComparer.Ordinal).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    /// <summary>The folder grouping key an entry's path implies, matching the evidence's key.</summary>
    public static string KeyFor(string path, string rootPath) =>
        Path.GetDirectoryName(path) is { } parent && RelativePath.Below(rootPath, parent) is { } relative
            ? relative.Replace(Path.DirectorySeparatorChar, '/')
            : string.Empty;

    /// <summary>
    /// Re-resolves the tracks a folder can now speak for, and reports how many were rescued.
    /// </summary>
    /// <remarks>
    /// The folder is everything in it, not only what this scan happened to read: unchanged
    /// siblings are answered from the index and never re-scanned, and without their votes a new
    /// file dropped into an established folder of mazurkas saw a folder of one. A sibling the
    /// folder itself answered is not one of them, though: its dance is the folder's own verdict
    /// played back, and once the file that really said "mazurka" is gone, the siblings it spoke
    /// for would go on agreeing with each other with nothing left behind them.
    /// </remarks>
    public static int Apply(
        IReadOnlyCollection<ScannedFile> scanned,
        IReadOnlyDictionary<string, LibraryEntry> known,
        string rootPath,
        DanceListIndex dances,
        DeclaredDiscovery declared)
    {
        var scannedPaths = scanned.Select(file => file.File.FullName).ToHashSet(StringComparer.Ordinal);
        // A row whose file could not be reached does not get a vote. Otherwise the tracks on a dead
        // drive decide the dance of the ones that are still there.
        var knownSlugsByFolder = known.Values
            .Where(entry => entry.IsAvailable
                && entry.DanceSlug is not null
                && !IsFolderAgreement(entry.Dance)
                && !scannedPaths.Contains(entry.Path))
            .GroupBy(entry => KeyFor(entry.Path, rootPath), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.DanceSlug!).ToList(), StringComparer.Ordinal);

        var rescued = 0;
        foreach (var folder in scanned.GroupBy(file => file.Evidence.FolderKey ?? string.Empty, StringComparer.Ordinal))
        {
            var siblings = folder.ToList();
            var voices = siblings
                .Where(sibling => sibling.Resolution.DanceSlug is not null)
                .Select(sibling => sibling.Resolution.DanceSlug!)
                .Concat(knownSlugsByFolder.GetValueOrDefault(folder.Key, []))
                .ToList();

            var agreed = AgreedDance(voices);
            if (agreed is null)
            {
                continue;
            }

            foreach (var sibling in siblings.Where(s => s.Resolution.DanceSlug is null))
            {
                var resolution = TrackInformationResolver.Resolve(
                    sibling.Evidence, dances, declared, agreed);
                if (resolution.DanceSlug is null)
                {
                    continue;
                }

                sibling.Resolution = resolution;
                rescued++;
            }
        }

        return rescued;
    }

    /// <summary>Whether a stored dance is one this class gave, rather than one a file said.</summary>
    /// <remarks>
    /// The index keeps where a value came from by kind and detail, not the claim that carried it,
    /// so the detail is what tells folder agreement apart from a folder level the user declared.
    /// </remarks>
    private static bool IsFolderAgreement(DerivedFrom from) =>
        from.Kind == ClaimSource.FolderAgreement.Kind
        && string.Equals(from.Detail, ClaimSource.FolderAgreement.Detail, StringComparison.Ordinal);
}
