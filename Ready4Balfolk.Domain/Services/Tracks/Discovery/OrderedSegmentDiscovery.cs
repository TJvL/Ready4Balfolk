namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class OrderedSegmentDiscovery
{
    private readonly IPatternSegmentDiscovery[] _ordered;

    public OrderedSegmentDiscovery(IEnumerable<IPatternSegmentDiscovery> patternSegmentDiscoveries)
    {
        _ordered = [.. patternSegmentDiscoveries.OrderBy(key => key switch
        {
            IDiscoveryOrder order => order.Order,
            _ => int.MaxValue
        })];
    }

    public ICollection<IPatternSegmentDiscovery> PatternSegmentDiscoveries => _ordered;
}
