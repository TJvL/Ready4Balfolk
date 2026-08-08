using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Services.Dances;

/// <summary>The shape of the <c>dances.json</c> that BigBalfolkList publishes.</summary>
/// <remarks>
/// Deliberately separate from <see cref="Models.Dances.DanceList"/>. This is somebody else's file
/// format, read once at import; binding the application's own model to it would make every later
/// change to that project a change here.
/// </remarks>
internal sealed record BigBalfolkListDocument
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; init; }

    [JsonPropertyName("dances")]
    public IReadOnlyList<BigBalfolkListDance> Dances { get; init; } = [];
}

internal sealed record BigBalfolkListDance
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("names")]
    public IReadOnlyList<string> Names { get; init; } = [];

    [JsonPropertyName("region")]
    public string? Region { get; init; }

    [JsonPropertyName("family")]
    public string? Family { get; init; }

    [JsonPropertyName("suite")]
    public string? Suite { get; init; }
}
