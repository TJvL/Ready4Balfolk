namespace Ready4Balfolk.Domain.Models.Tracks;

public record DiscoveryPattern(ICollection<string> Pattern)
{
    public static readonly DiscoveryPattern DefaultDefault = new(["%d - %a - %t.%x"]);
    public static readonly DiscoveryPattern ExtendedDefault = new(["%a", "%l", "%n - %t.%x"]);

    public static implicit operator string[](DiscoveryPattern pattern) => [.. pattern.Pattern];
}
