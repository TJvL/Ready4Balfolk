using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Tree;

public sealed record DanceBranch
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("weight")]
    public required int Weight { get; init; }

    [JsonPropertyName("children")]
    public IEnumerable<DanceBranch> Branches { get; init; } = [];

    [JsonPropertyName("dances")]
    public IEnumerable<DanceLeaf> Leafs { get; init; } = [];
}
