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
    /// <exception cref="DanceListRefusedException">
    /// Unparseable, the wrong format version, or invalid. Its message is the English line for the
    /// log, and its screen text is what the DJ is told.
    /// </exception>
    public static DanceList Read(string json)
    {
        DanceList? list;
        try
        {
            list = JsonSerializer.Deserialize<DanceList>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new DanceListRefusedException(
                "The dance list is not readable JSON", DomainStrings.DanceList_InvalidJson, exception);
        }

        if (list is null)
        {
            throw new DanceListRefusedException("The dance list is empty", DomainStrings.DanceList_NoDances);
        }

        // The version first, because a file in a dead format has no dances where this one looks
        // for them and would otherwise be reported as empty. An older file is a format that no
        // longer exists, and a newer one says something this build has no way to understand;
        // guessing at either is worse than keeping the list already loaded.
        if (list.FormatVersion != DanceList.CurrentFormatVersion)
        {
            throw new DanceListRefusedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The dance list is format version {0}, and this build reads version {1}",
                    list.FormatVersion,
                    DanceList.CurrentFormatVersion),
                string.Format(
                    CultureInfo.CurrentCulture,
                    DomainStrings.DanceList_WrongFormatVersion,
                    list.FormatVersion,
                    DanceList.CurrentFormatVersion));
        }

        if (list.IsEmpty)
        {
            throw new DanceListRefusedException("The dance list has no dances", DomainStrings.DanceList_NoDances);
        }

        var problems = DanceListValidation.Validate(list);
        if (problems.DuplicateNames.Count > 0)
        {
            // A name meaning two dances is what makes discovery ambiguous, so it is named out loud.
            var names = string.Join(", ", problems.DuplicateNames.Distinct(StringComparer.Ordinal));
            throw new DanceListRefusedException(
                $"The dance list uses one name for more than one dance: {names}",
                string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceList_DuplicateNames, names));
        }

        return problems.Any
            ? throw new DanceListRefusedException(
                "The dance list breaks the rules the list is built on", DomainStrings.DanceList_Invalid)
            : list;
    }
}
