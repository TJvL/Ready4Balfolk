using System.Globalization;
using System.Text.Json;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>Turns the list BigBalfolkList publishes into the user's own dance list, once.</summary>
/// <remarks>
/// Nothing is embedded in the application and nothing layers on top of the result. After this runs,
/// the list belongs to the user and this importer has no further say in it.
/// </remarks>
public static class BigBalfolkListImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Reads a BigBalfolkList <c>dances.json</c> and builds a dance list from it.</summary>
    /// <remarks>
    /// The file's own structure becomes the categories: a region, and inside it the family or suite
    /// when the dance names one. Everything starts at weight 1, because the file asserts what these
    /// dances are, not how often this user wants to hear them.
    /// </remarks>
    /// <exception cref="FileNotFoundException">The file is not there.</exception>
    /// <exception cref="InvalidDataException">The file is not a readable BigBalfolkList export.</exception>
    public static async Task<DanceList> ReadAsync(FileInfo sourceFileInfo, CancellationToken token = default)
    {
        if (!sourceFileInfo.Exists)
        {
            throw new FileNotFoundException(DomainStrings.ImportFileNotFound, sourceFileInfo.FullName);
        }

        BigBalfolkListDocument document;
        try
        {
            await using var stream = File.OpenRead(sourceFileInfo.FullName);
            document = await JsonSerializer.DeserializeAsync<BigBalfolkListDocument>(stream, JsonOptions, token)
                       ?? throw new InvalidDataException(DomainStrings.ImportFileContainsNull);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(DomainStrings.BigBalfolkList_InvalidJson, exception);
        }

        if (document.FormatVersion < 1 || document.Dances.Count == 0)
        {
            throw new InvalidDataException(DomainStrings.BigBalfolkList_NotADanceFile);
        }

        var list = Convert(document);

        var problems = DanceListValidation.Validate(list);
        if (problems.DuplicateNames.Count > 0)
        {
            // BigBalfolkList's own build fails on this, so a file that reaches here with a collision
            // has been edited by hand. Refusing it is the point: an ambiguous name is exactly what
            // makes discovery answer with a set instead of a dance.
            throw new InvalidDataException(string.Format(
                CultureInfo.CurrentCulture,
                DomainStrings.BigBalfolkList_DuplicateNames,
                string.Join(", ", problems.DuplicateNames.Distinct(StringComparer.Ordinal))));
        }

        return list;
    }

    private static DanceList Convert(BigBalfolkListDocument document)
    {
        // Insertion-ordered throughout: the published file is authored in a sensible order and
        // there is no better one to invent.
        var regions = new List<PendingCategory>();

        foreach (var entry in document.Dances)
        {
            var names = entry.Names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (string.IsNullOrWhiteSpace(entry.Slug) || names.Count == 0)
            {
                continue;
            }

            var dance = new Dance
            {
                Slug = entry.Slug.Trim(),
                Names = names,
                Weight = 1
            };

            var regionName = string.IsNullOrWhiteSpace(entry.Region)
                ? DomainStrings.DanceList_UncategorisedCategory
                : entry.Region.Trim();
            var region = FindOrAdd(regions, regionName);

            // Family and suite are mutually exclusive in the published file, and either one means
            // the same thing here: a named group of dances inside a region.
            var groupName = string.IsNullOrWhiteSpace(entry.Family)
                ? string.IsNullOrWhiteSpace(entry.Suite) ? null : entry.Suite.Trim()
                : entry.Family.Trim();

            var target = groupName is null ? region : FindOrAdd(region.Children, groupName);
            target.Dances.Add(dance);
        }

        return new DanceList
        {
            Categories = [.. regions.Select(ToCategory)]
        };
    }

    private static PendingCategory FindOrAdd(List<PendingCategory> categories, string name)
    {
        var existing = categories.Find(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var added = new PendingCategory(name);
        categories.Add(added);
        return added;
    }

    private static DanceCategory ToCategory(PendingCategory pending) => new()
    {
        Name = pending.Name,
        Weight = 1,
        Dances = pending.Dances,
        Categories = [.. pending.Children.Select(ToCategory)]
    };

    /// <summary>A category being accumulated, before it is frozen into an immutable one.</summary>
    private sealed class PendingCategory(string name)
    {
        public string Name { get; } = name;

        public List<Dance> Dances { get; } = [];

        public List<PendingCategory> Children { get; } = [];
    }
}
