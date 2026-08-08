using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>A dance in the user's list.</summary>
/// <remarks>
/// The slug is the identity. The names are a flat set of equals, because the spelling of a balfolk
/// dance is genuinely contested and often has no right answer; the first one is simply the one that
/// gets displayed. Reordering the names is therefore how a user chooses which spelling they read,
/// and it moves nothing else, because everything that refers to a dance refers to its slug.
/// </remarks>
public sealed record Dance
{
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("names")]
    public required IReadOnlyList<string> Names { get; init; }

    [JsonPropertyName("weight")]
    public int Weight { get; init; } = 1;

    /// <summary>The name shown for this dance: the first one, falling back to the slug.</summary>
    [JsonIgnore]
    public string DisplayName => Names.Count > 0 ? Names[0] : Slug;
}
