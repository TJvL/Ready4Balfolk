using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class DanceFileDiscovery(IDanceFileDiscoveryService danceFileDiscovery)
    : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public int Order => 2;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo)
    {
        if (fileInfo.Directory == null)
        {
            yield break;
        }

        var fileMatches = danceFileDiscovery.Matches(fileInfo.Directory);
        if (!fileMatches.TryGetValue(fileInfo.Name, out var dance))
        {
            yield break;
        }

        yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, dance);
    }

}
