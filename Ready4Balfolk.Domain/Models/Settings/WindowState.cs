using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Settings;

public sealed record WindowState(
    [property: JsonPropertyName("X")] double? X = null,
    [property: JsonPropertyName("Y")] double? Y = null,
    [property: JsonPropertyName("Width")] double? Width = null,
    [property: JsonPropertyName("Height")] double? Height = null,
    [property: JsonPropertyName("IsMaximized")] bool IsMaximized = false,
    [property: JsonPropertyName("IsBorderless")] bool IsBorderless = false);
