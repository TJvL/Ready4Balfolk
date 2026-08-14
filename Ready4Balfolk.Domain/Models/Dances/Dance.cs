using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>A dance, in exactly the shape BigBalfolkList publishes.</summary>
/// <remarks>
/// <para>
/// The slug is the identity. The names are a flat set of equals, because the spelling of a balfolk
/// dance is genuinely contested and often has no right answer; the first one is simply the one that
/// gets displayed. Everything that refers to a dance refers to its slug, so a respelling upstream
/// costs nothing here.
/// </para>
/// <para>
/// Everything else is a tag: where the dance comes from, which family it belongs to, whether it is
/// danced as part of a suite. There is no hierarchy, so a dance is never filed under one of them at
/// the expense of the others.
/// </para>
/// </remarks>
public sealed record Dance
{
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("names")]
    public required IReadOnlyList<string> Names { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The name shown for this dance: the first one, falling back to the slug.</summary>
    [JsonIgnore]
    public string DisplayName => Names.Count > 0 ? Names[0] : Slug;

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);
}
