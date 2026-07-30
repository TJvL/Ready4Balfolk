using System.IO.Abstractions;
using Ready4Balfolk.Domain.Exceptions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public sealed class TrackInformationDiscoveryService(OrderedSegmentDiscovery patternSegmentDiscoveries)
    : ITrackDiscoveryService
{
    public Track LoadTrack(IFileInfo fileInfo) => LoadTrackWithDuration(fileInfo, GetTrackDuration(fileInfo));

    public Track LoadTrackWithDuration(IFileInfo fileInfo, TimeSpan duration)
    {
        var set = LoadMinimalSet(fileInfo);
        var format = AudioFormatInformation.ParseAudioFormat(fileInfo.Extension);
        return Track.FromSegments(set, fileInfo, duration, format);
    }

    // Title and Artist are required; Dance is wanted but a track without one is
    // still created (and filtered later), so it must not make discovery throw.
    private static readonly HashSet<PatternSegment> MinimalSet = [PatternSegment.Title, PatternSegment.Artist];
    private static readonly HashSet<PatternSegment> DesiredSet = [PatternSegment.Title, PatternSegment.Artist, PatternSegment.Dance];

    public Dictionary<PatternSegment, string> LoadMinimalSet(IFileInfo fileInfo)
    {
        Dictionary<PatternSegment, string> patterns = [];
        foreach (var patternSegmentDiscovery in patternSegmentDiscoveries.PatternSegmentDiscoveries)
        {
            var discoveredPatterns = patternSegmentDiscovery.Scan(fileInfo);
            foreach (var (segment, value) in discoveredPatterns)
            {
                patterns.TryAdd(segment, value);
            }

            // Only stop early once the dance is also known; otherwise keep
            // consulting later steps (dances.json, filename pattern) so a file
            // with ordinary Title/Artist tags still gets its dance discovered.
            if (DesiredSet.All(patterns.ContainsKey))
            {
                return patterns;
            }
        }

        return MinimalSet.All(patterns.ContainsKey)
            ? patterns
            : throw new TrackInformationDiscoveryException($"Minimal set of information not found for song: {fileInfo.FullName}");
    }

    private static TimeSpan GetTrackDuration(IFileInfo fileInfo)
    {
        try
        {
            using var file = TagLib.File.Create(fileInfo.FullName);
            return file.Properties.Duration;
        }
        catch (Exception exception) when (exception is not IOException and not FormatException)
        {
            throw new IOException($"Unable to load track duration for '{fileInfo.FullName}'", exception);
        }
    }
}
