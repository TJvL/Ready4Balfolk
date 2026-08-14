using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public interface IPatternSegmentDiscovery
{
    IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo);
}
