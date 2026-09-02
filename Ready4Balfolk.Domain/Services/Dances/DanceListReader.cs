using System.Globalization;
using System.Text.Json;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>Reads a published <c>dances.json</c>, from wherever it arrived.</summary>
/// <remarks>
/// One reader for all three sources: the cached copy on disk, a download, and a file the user
/// picked to update from offline. They are the same bytes in the same format, so a file that would
/// be refused from one is refused from all of them.
/// </remarks>
public static class DanceListReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parses and checks a list. Refusing is the point: read as an empty list, a truncated download
    /// would leave the application with no vocabulary and no sign that anything went wrong.
    /// </summary>
    /// <exception cref="InvalidDataException">Unparseable, the wrong format version, or invalid.</exception>
    public static DanceList Read(string json)
    {
        DanceList? list;
        try
        {
            list = JsonSerializer.Deserialize<DanceList>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(DomainStrings.DanceList_InvalidJson, exception);
        }

        if (list is null)
        {
            throw new InvalidDataException(DomainStrings.DanceList_NoDances);
        }

        // The version first, because a file in a dead format has no dances where this one looks
        // for them and would otherwise be reported as empty. An older file is a format that no
        // longer exists, and a newer one says something this build has no way to understand;
        // guessing at either is worse than keeping the list already loaded.
        if (list.FormatVersion != DanceList.CurrentFormatVersion)
        {
            throw new InvalidDataException(string.Format(
                CultureInfo.CurrentCulture,
                DomainStrings.DanceList_WrongFormatVersion,
                list.FormatVersion,
                DanceList.CurrentFormatVersion));
        }

        if (list.IsEmpty)
        {
            throw new InvalidDataException(DomainStrings.DanceList_NoDances);
        }

        var problems = DanceListValidation.Validate(list);
        if (problems.DuplicateNames.Count > 0)
        {
            // A name meaning two dances is what makes discovery ambiguous, so it is named out loud.
            throw new InvalidDataException(string.Format(
                CultureInfo.CurrentCulture,
                DomainStrings.DanceList_DuplicateNames,
                string.Join(", ", problems.DuplicateNames.Distinct(StringComparer.Ordinal))));
        }

        return problems.Any ? throw new InvalidDataException(DomainStrings.DanceList_Invalid) : list;
    }
}
