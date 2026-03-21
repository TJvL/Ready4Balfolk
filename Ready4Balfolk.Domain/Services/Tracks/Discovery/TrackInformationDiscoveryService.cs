using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public sealed class TrackInformationDiscoveryService : ITrackDiscoveryService
{
    private readonly OrderedSegmentDiscovery _patternSegmentDiscoveries;

    public TrackInformationDiscoveryService(OrderedSegmentDiscovery patternSegmentDiscoveries)
    {
        _patternSegmentDiscoveries = patternSegmentDiscoveries;
    }

    public Track LoadTrack(IFileInfo fileInfo) => LoadTrackWithDuration(fileInfo, GetTrackDuration(fileInfo));

    public Track LoadTrackWithDuration(IFileInfo fileInfo, TimeSpan duration)
    {
        var set = LoadMinimalSet(fileInfo);
        var format = AudioFormatInformation.ParseAudioFormat(fileInfo.Extension);
        return Track.FromSegments(set, fileInfo, duration, format);
    }

    public Dictionary<PatternSegment, string> LoadMinimalSet(IFileInfo fileInfo)
    {
        HashSet<PatternSegment> minimalSet = [PatternSegment.Dance, PatternSegment.Title, PatternSegment.Artist];
        Dictionary<PatternSegment, string> patterns = [];
        foreach (var patternSegmentDiscovery in _patternSegmentDiscoveries.PatternSegmentDiscoveries)
        {
            var discoveredPatterns = patternSegmentDiscovery.Scan(fileInfo);
            foreach (var (segment, value) in discoveredPatterns)
            {
                patterns.TryAdd(segment, value);
            }

            if (minimalSet.All(patterns.ContainsKey))
            {
                return patterns;
            }
        }

        // Last effort
        patterns.TryAdd(PatternSegment.Dance, "Not-Found");
        if (minimalSet.All(patterns.ContainsKey))
        {
            return patterns;
        }

        throw new Exception($"Minimal set of information not found for song: {fileInfo.FullName}");
    }

    private static TimeSpan GetTrackDuration(IFileInfo fileInfo)
    {
        try
        {
            using var file = TagLib.File.Create(fileInfo.FullName);
            return file.Properties.Duration;
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"Unable to load track duration for '{fileInfo.Name}'.", exception);
        }
    }
}
