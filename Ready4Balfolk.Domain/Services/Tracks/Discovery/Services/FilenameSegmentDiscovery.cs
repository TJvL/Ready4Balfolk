using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class FilenameSegmentDiscovery(DiscoveryPattern discoveryPattern) : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public int Order => 3;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo) => PatternMatcher.Match(discoveryPattern, fileInfo);
}
