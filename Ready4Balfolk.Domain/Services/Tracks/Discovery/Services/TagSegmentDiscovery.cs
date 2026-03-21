using System.Globalization;
using System.IO.Abstractions;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class TagSegmentDiscovery : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public int Order => 1;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo)
    {
        using var file = TagLib.File.Create(fileInfo.FullName);
        if (!string.IsNullOrWhiteSpace(file.Tag.Album))
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Album, file.Tag.Album);
        }
        if (!string.IsNullOrWhiteSpace(file.Tag.FirstPerformer))
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Artist, file.Tag.FirstPerformer);
        }
        if (file.Tag.Track > 0)
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.TrackNumber, file.Tag.Track.ToString("D", CultureInfo.InvariantCulture));
        }
        if (file.Tag.Year > 0)
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Year, file.Tag.Year.ToString("D", CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(file.Tag.Title))
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Title, file.Tag.Title);
        }
        if (!string.IsNullOrWhiteSpace(file.Tag.FirstGenre))
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Genre, file.Tag.FirstGenre);
        }

        var dances = file.GetCustomTag("dance");
        if (dances?.Length > 0)
        {
            yield return  new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, dances.First());
        }
    }
}
