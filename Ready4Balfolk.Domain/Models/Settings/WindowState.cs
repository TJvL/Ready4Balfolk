namespace Ready4Balfolk.Domain.Models.Settings;

public sealed record WindowState(
    double? X = null,
    double? Y = null,
    double? Width = null,
    double? Height = null,
    bool IsMaximized = false,
    bool IsBorderless = false);
