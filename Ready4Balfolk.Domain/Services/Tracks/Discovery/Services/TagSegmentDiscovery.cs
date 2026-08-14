using System.Globalization;
using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class TagSegmentDiscovery(ITagFileFactory tagFileFactory) : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public int Order => 1;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo)
    {
        using var file = tagFileFactory.Create(fileInfo);
        if (!string.IsNullOrWhiteSpace(file.Album))
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Album, file.Album);
        }
        if (!string.IsNullOrWhiteSpace(file.Artist))
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Artist, file.Artist);
        }
        if (file.Track > 0)
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.TrackNumber, file.Track.ToString("D", CultureInfo.InvariantCulture));
        }
        if (file.Year > 0)
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Year, file.Year.ToString("D", CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(file.Title))
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Title, file.Title);
        }
        if (!string.IsNullOrWhiteSpace(file.Genre))
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Genre, file.Genre);
        }

        if (!string.IsNullOrWhiteSpace(file.Dance))
        {
            yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, file.Dance);
        }
    }
}
