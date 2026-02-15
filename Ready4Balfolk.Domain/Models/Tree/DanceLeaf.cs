using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Tree;

public sealed record DanceLeaf(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("weight")] int Weight);
