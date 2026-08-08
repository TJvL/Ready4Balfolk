using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>A branch of the dance list: named, weighted, and holding dances and sub-categories.</summary>
/// <remarks>
/// There is no separate tree structure. Randomisation reads these categories directly, so a pick is
/// weighted by the category's weight multiplied by the dance's, and marking a category as the random
/// scope picks within it. Nesting exists because an imported list has two levels: a region, and
/// inside it a family or a suite.
/// </remarks>
public sealed record DanceCategory
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;

    [JsonPropertyName("categories")]
    public IReadOnlyList<DanceCategory> Categories { get; init; } = [];

    [JsonPropertyName("dances")]
    public IReadOnlyList<Dance> Dances { get; init; } = [];
}
