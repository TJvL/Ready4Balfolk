using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class FilenameSegmentDiscovery(Func<DiscoveryPattern> discoveryPattern) : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public FilenameSegmentDiscovery(DiscoveryPattern discoveryPattern)
        : this(() => discoveryPattern)
    {
    }

    public int Order => 3;

    // The pattern is read per scan so settings changes take effect without an
    // application restart.
    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo) => PatternMatcher.Match(discoveryPattern(), fileInfo);
}
