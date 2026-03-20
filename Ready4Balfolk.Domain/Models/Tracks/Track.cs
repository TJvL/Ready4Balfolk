using System.IO.Abstractions;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Domain.Models.Tracks;

public sealed record Track(string Dance, string Artist, string Title, IFileInfo FileInfo, TimeSpan Length, AudioFormat Format)
{
    public string OriginalDance { get; init; } = Dance;

    public static Track FromSegments(Dictionary<PatternSegment, string> segments, IFileInfo fileInfo, TimeSpan length, AudioFormat format)
    {
        string GetOrDefault(PatternSegment key) =>
            segments.GetValueOrDefault(key, "");

        var dance = GetOrDefault(PatternSegment.Dance);
        var artist = GetOrDefault(PatternSegment.Artist);
        var title = GetOrDefault(PatternSegment.Title);

        return new Track(dance, artist, title, fileInfo, length, format);
    }
}
