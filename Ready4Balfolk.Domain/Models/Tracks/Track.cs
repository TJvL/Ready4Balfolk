using System.IO.Abstractions;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Domain.Models.Tracks;

public sealed record Track(string Dance, string Artist, string Title, IFileInfo FileInfo, TimeSpan Length, AudioFormat Format)
{
    public string OriginalDance { get; init; } = Dance;

    public static Track FromSegments(Dictionary<PatternSegment, string> segments, IFileInfo fileInfo, TimeSpan length, AudioFormat format)
    {
        var dance = segments.GetValueOrDefault(PatternSegment.Dance, "");
        var artist = segments.GetValueOrDefault(PatternSegment.Artist, "");
        var title = segments.GetValueOrDefault(PatternSegment.Title, "");

        return new Track(dance, artist, title, fileInfo, length, format);
    }

    public bool IsValid()
    {
        var validDance = !string.IsNullOrWhiteSpace(Dance);
        var validTitle = !string.IsNullOrWhiteSpace(Title);
        var validArtist = !string.IsNullOrWhiteSpace(Artist);
        var validDuration = Length >= TimeSpan.Zero;
        var fileInfo = FileInfo.Exists;

        return validDance && validTitle && validArtist && validDuration && fileInfo;
    }
}
