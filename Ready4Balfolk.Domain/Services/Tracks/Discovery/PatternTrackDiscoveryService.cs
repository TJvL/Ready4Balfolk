using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public sealed class PatternTrackDiscoveryService : ITrackDiscoveryService
{
    private readonly DiscoveryPattern _discoveryPattern;
    private readonly IDanceFileDiscoveryService? _danceFileDiscovery;

    public PatternTrackDiscoveryService(DiscoveryPattern discoveryPattern, IDanceFileDiscoveryService? danceFileDiscovery)
    {
        _discoveryPattern = discoveryPattern;
        _danceFileDiscovery = danceFileDiscovery;
    }

    public Track LoadTrack(IFileInfo fileInfo)
    {
        var duration = GetTrackDuration(fileInfo);

        return LoadTrackWithDuration(fileInfo, duration);
    }

    public Track LoadTrackWithDuration(IFileInfo fileInfo, TimeSpan duration)
    {
        var match = PatternMatcher.Match(_discoveryPattern, fileInfo);

        if (!match.TryGetValue(PatternSegment.Extension, out var value))
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
        }

        var format = AudioFormatInformation.ParseAudioFormat(value);

        if (!match.ContainsKey(PatternSegment.Dance) &&
            _danceFileDiscovery is not null &&
            fileInfo.Directory is not null)
        {
            var dance = _danceFileDiscovery.Matches(fileInfo.Directory).GetValueOrDefault(fileInfo.Name);
            if (dance is not null)
            {
                match[PatternSegment.Dance] = dance;
            }
        }

        return Track.FromSegments(match, fileInfo, duration, format);
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

public static class AudioFormatInformation
{
    private static readonly Dictionary<string, AudioFormat> SupportedFormatLookup = new Dictionary<string, AudioFormat>()
    {
        { ".mp1", AudioFormat.Mp3 },
        { ".mp2", AudioFormat.Mp3 },
        { ".mp3", AudioFormat.Mp3 },
        { ".wav", AudioFormat.Wav },
        { ".flac", AudioFormat.Flac },
        { ".fla", AudioFormat.Flac },
        { ".ogg", AudioFormat.Ogg },
        { ".oga", AudioFormat.Ogg },
        { ".aif", AudioFormat.Aif },
        { ".aiff", AudioFormat.Aif },
    };

    public static HashSet<string> SupportedFormats => SupportedFormatLookup.Keys.ToHashSet();

    public static AudioFormat ParseAudioFormat(string extension)
    {
        if (!SupportedFormatLookup.TryGetValue($".{extension}".ToLowerInvariant(), out var format))
        {
            throw new ArgumentOutOfRangeException(nameof(extension), extension, $"Unsupported audio format for '{extension}'.");
        }

        return format;
    }
}
