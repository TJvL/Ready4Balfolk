using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>The dance list, exactly as BigBalfolkList publishes it.</summary>
/// <remarks>
/// This is shared vocabulary, not user content: the application reads it and never writes it. The
/// file on disk is a copy of somebody else's file, byte for byte, so an update is a replacement
/// rather than a merge and there is nothing of the user's in it to lose.
/// </remarks>
public sealed record DanceList
{
    /// <summary>The format version BigBalfolkList publishes. Anything else is refused.</summary>
    public const int CurrentFormatVersion = 3;

    public static DanceList Empty { get; } = new();

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>Every tag that exists, including ones no dance carries yet.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("dances")]
    public IReadOnlyList<Dance> Dances { get; init; } = [];

    [JsonIgnore]
    public bool IsEmpty => Dances.Count == 0;

    public Dance? FindDance(string slug) =>
        Dances.FirstOrDefault(dance => string.Equals(dance.Slug, slug, StringComparison.Ordinal));

    /// <summary>How many dances carry a tag, which is what sizes it in the tag cloud.</summary>
    public int CountOf(string tag) => Dances.Count(dance => dance.HasTag(tag));

    /// <summary>
    /// The dances a set of tags reaches. An empty set reaches everything, which is what makes the
    /// empty pool mean "anything at all" rather than "nothing".
    /// </summary>
    public IEnumerable<Dance> WithAnyTag(IReadOnlyCollection<string> tags) =>
        tags.Count == 0 ? Dances : Dances.Where(dance => tags.Any(dance.HasTag));
}
