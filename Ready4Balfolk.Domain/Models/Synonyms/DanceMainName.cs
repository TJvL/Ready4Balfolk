using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Synonyms;

public sealed record DanceMainName(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("synonyms")]
    IEnumerable<DanceSynonym> Synonyms);
