using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>The user's own list of dances: the one place dance names live.</summary>
/// <remarks>
/// Nothing ships with the application and nothing layers on top of this. The list is built once, by
/// hand or by importing the format BigBalfolkList publishes, and after that it belongs to the user.
/// </remarks>
public sealed record DanceList
{
    /// <summary>Bumped when the on-disk shape changes in a way older files cannot be read as.</summary>
    public const int CurrentFormatVersion = 1;

    public static DanceList Empty { get; } = new();

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    [JsonPropertyName("categories")]
    public IReadOnlyList<DanceCategory> Categories { get; init; } = [];

    /// <summary>Every dance in the list, in category order, however deeply nested.</summary>
    [JsonIgnore]
    public IEnumerable<Dance> AllDances => EnumerateDances(Categories);

    [JsonIgnore]
    public bool IsEmpty => !AllDances.Any();

    private static IEnumerable<Dance> EnumerateDances(IReadOnlyList<DanceCategory> categories)
    {
        foreach (var category in categories)
        {
            foreach (var dance in category.Dances)
            {
                yield return dance;
            }

            foreach (var dance in EnumerateDances(category.Categories))
            {
                yield return dance;
            }
        }
    }
}
