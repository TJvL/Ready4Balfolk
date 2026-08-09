using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>Builds the report from what is in the index.</summary>
/// <remarks>
/// Built from the index rather than accumulated during a scan, so it is the same report whether the
/// scan just ran or the application was restarted. The badge is a query, not a number somebody has
/// to remember to keep up to date.
/// </remarks>
public static class ScanReportBuilder
{
    public static ScanReport Build(
        IReadOnlyCollection<LibraryEntry> entries,
        DanceListIndex index,
        IReadOnlySet<string> ignoredValues,
        int unreadable = 0,
        int unsupported = 0)
    {
        var resolved = entries.Where(entry => entry.DanceSlug is not null).ToList();

        var resolvedCountBySlug = resolved
            .GroupBy(entry => entry.DanceSlug!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var resolvedByFolder = resolved
            .GroupBy(entry => FolderOf(entry.Path), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.GroupBy(entry => entry.DanceSlug!, StringComparer.Ordinal)
                    .ToDictionary(inner => inner.Key, inner => inner.Count(), StringComparer.Ordinal),
                StringComparer.Ordinal);

        var unresolved = entries.Where(entry => entry.DanceSlug is null).ToList();

        var grouped = unresolved
            .Where(entry => !string.IsNullOrWhiteSpace(entry.OriginalDance))
            .Where(entry => !ignoredValues.Contains(StringNormalizer.Normalize(entry.OriginalDance!)))
            .GroupBy(entry => entry.OriginalDance!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => Describe(group.Key, [.. group], index, resolvedCountBySlug, resolvedByFolder))
            // Most tracks first: the decision that settles the most files is the one worth making.
            .OrderByDescending(value => value.TrackCount)
            .ThenBy(value => value.Value, StringComparer.CurrentCulture)
            .ToList();

        return new ScanReport
        {
            Complete = resolved.Count,
            Unreadable = unreadable,
            Unsupported = unsupported,
            Unrecognised = grouped,
            SilentlyUnresolved = unresolved.Count(entry => string.IsNullOrWhiteSpace(entry.OriginalDance))
        };
    }

    private static UnrecognisedValue Describe(
        string value,
        IReadOnlyList<LibraryEntry> entries,
        DanceListIndex index,
        Dictionary<string, int> resolvedCountBySlug,
        Dictionary<string, Dictionary<string, int>> resolvedByFolder)
    {
        var (kind, slugs) = UnrecognisedValueClassifier.Classify(value, index);

        var suggestions = slugs
            .Select(slug => new DanceSuggestion(
                slug, index.DisplayNameFor(slug), resolvedCountBySlug.GetValueOrDefault(slug)))
            // Ranked by what the library already resolved to, not alphabetically: the dance this
            // user actually plays belongs at the top.
            .OrderByDescending(suggestion => suggestion.TrackCount)
            .ThenBy(suggestion => suggestion.DisplayName, StringComparer.CurrentCulture)
            .ToList();

        var folders = entries
            .GroupBy(entry => FolderOf(entry.Path), StringComparer.Ordinal)
            .Select(group => new FolderBreakdown(
                group.Key,
                [.. group.Select(entry => entry.Path)],
                FolderSuggestions(group.Key, index, resolvedByFolder)))
            .OrderByDescending(folder => folder.Suggestions.Count)
            .ThenByDescending(folder => folder.Paths.Count)
            .ToList();

        return new UnrecognisedValue
        {
            Value = value,
            Kind = kind,
            Paths = [.. entries.Select(entry => entry.Path)],
            Suggestions = suggestions,
            Folders = folders
        };
    }

    private static IReadOnlyList<DanceSuggestion> FolderSuggestions(
        string folderKey, DanceListIndex index, Dictionary<string, Dictionary<string, int>> resolvedByFolder)
    {
        return !resolvedByFolder.TryGetValue(folderKey, out var counts)
            ? []
            :
            [
                .. counts
                    .OrderByDescending(pair => pair.Value)
                    .Select(pair => new DanceSuggestion(pair.Key, index.DisplayNameFor(pair.Key), pair.Value))
            ];
    }

    private static string FolderOf(string path) => Path.GetDirectoryName(path) ?? string.Empty;
}
