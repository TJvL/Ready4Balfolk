using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class FilenameSegmentDiscovery : IPatternSegmentDiscovery, IDiscoveryOrder
{
    private readonly DiscoveryPattern _discoveryPattern;

    public FilenameSegmentDiscovery(DiscoveryPattern discoveryPattern)
    {
        _discoveryPattern = discoveryPattern;
    }

    public int Order => 3;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo) => PatternMatcher.Match(_discoveryPattern, fileInfo);
}