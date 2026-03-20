using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoreLinq;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class PatternTrackDiscoveryService : ITrackDiscoveryService
{
    private readonly DiscoveryPattern _discoveryPattern;
    private readonly DanceFileDiscovery? _danceFileDiscovery;

    public PatternTrackDiscoveryService(DiscoveryPattern discoveryPattern, DanceFileDiscovery? danceFileDiscovery)
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

        var format = ParseAudioFormat(value);

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

    private static AudioFormat ParseAudioFormat(string ext)
    {
        return $".{ext}".ToLowerInvariant() switch
        {
            ".mp3" or ".mp2" or ".mp1" => AudioFormat.Mp3,
            ".wav" => AudioFormat.Wav,
            ".flac" or ".fla" => AudioFormat.Flac,
            ".ogg" or ".oga" => AudioFormat.Ogg,
            ".aif" or ".aiff" => AudioFormat.Aif,
            _ => throw new ArgumentOutOfRangeException(nameof(ext), ext, $"Unsupported audio format for '{ext}'.")
        };
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

public class DanceFileDiscovery : IDanceFileDiscovery
{
    private readonly IFileSystem _fileSystem;

    public DanceFileDiscovery(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool Exists(IDirectoryInfo directoryInfo) => FileInfo(directoryInfo).Exists;

    public Dictionary<string, string> Matches(IDirectoryInfo directoryInfo)
    {
        var fileInfo = FileInfo(directoryInfo);
        if (!fileInfo.Exists)
        {
            return [];
        }

        using var stream = fileInfo.OpenRead();
        // Deserialize JSON from the stream
        var entries = JsonSerializer.Deserialize<ICollection<DanceFileEntry>>(stream);
        return entries switch
        {
            null => [],
            _ => entries.ToDictionary(r => r.FileName, r => r.Dance)
        };
    }

    private IFileInfo FileInfo(IDirectoryInfo directoryInfo)
    {
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory '{directoryInfo.FullName}' does not exist");
        }

        var filePath = _fileSystem.Path.Combine(directoryInfo.FullName, "dances.json");

        return _fileSystem.FileInfo.New(filePath);
    }

    public void Write(IDirectoryInfo directoryInfo, ICollection<DanceFileEntry> danceEntries)
    {
        var fileInfo = FileInfo(directoryInfo);

        // Write text to the file
        using var stream = fileInfo.Create();
        JsonSerializer.Serialize(stream, danceEntries, new JsonSerializerOptions { WriteIndented = true });
    }
}

public record DanceFileEntry(string FileName, string Dance);

public interface IDanceFileDiscovery
{
    Dictionary<string, string> Matches(IDirectoryInfo directoryInfo);
}

public record DiscoveryPattern(ICollection<string> Pattern)
{
    public static readonly DiscoveryPattern DefaultDefault = new(["%d - %a - %t.%x"]);
    public static readonly DiscoveryPattern ExtendedDefault = new(["%a", "%l", "%n - %t.%x"]);

    public static implicit operator string[](DiscoveryPattern pattern) => [.. pattern.Pattern];
}

public enum PatternSegment
{
    Artist,
    Album,
    Title,
    TrackNumber,
    Year,
    Genre,
    Dance,
    Extension
}

public class PatternMatcher
{
    private static readonly Dictionary<string, (PatternSegment Segment, string Pattern)> TokenMap = new()
    {
        { "%a", (PatternSegment.Artist, @".+?") },
        { "%l", (PatternSegment.Album, @".+?") },
        { "%t", (PatternSegment.Title, @".+?") },
        { "%n", (PatternSegment.TrackNumber, @"\d+") },
        { "%y", (PatternSegment.Year, @"\d{4}") },
        { "%g", (PatternSegment.Genre, @".+?") },

        { "%d", (PatternSegment.Dance, @".+?") },
        { "%x", (PatternSegment.Extension, @"\w{3}") }
    };

    // Cache compiled regex per pattern
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static Dictionary<PatternSegment, string> Match(DiscoveryPattern pattern, IFileInfo fileInfo)
    {
        var fileSegments = fileInfo.FullName.Split(Path.DirectorySeparatorChar);
        var patternSegments = pattern.Pattern;

        return Match(fileSegments, patternSegments);
    }

    static Dictionary<PatternSegment, string> Match(ICollection<string> fileSegments, ICollection<string> patternSegments)
    {
        // It doesn't matter what order we test this, as long as we go from the latest path
        var zipped = fileSegments.Reverse().ZipShortest(patternSegments.Reverse(), (fileSegment, patternSegment) => (fileSegment, patternSegment));

        return zipped.SelectMany(r => Match(r.patternSegment, r.fileSegment))
            .ToDictionary(r => r.Key, r => r.Value);
    }

    private static IEnumerable<KeyValuePair<PatternSegment, string>> Match(string pattern, string input)
    {
        var regex = Cache.GetOrAdd(pattern, BuildRegex);

        Match match;
        try
        {
            match = regex.Match(input);
        }
        catch (RegexMatchTimeoutException)
        {
            yield break;
        }

        if (!match.Success)
        {
            yield break;
        }

        foreach (var (segment, _) in TokenMap.Values)
        {
            var segmentName = segment.ToString("G");
            if (match.Groups[segmentName].Success)
            {
                yield return new KeyValuePair<PatternSegment, string>(segment, match.Groups[segmentName].Value);
            }
        }
    }

    private static Regex BuildRegex(string pattern)
    {
        var regexPattern = Regex.Escape(pattern);
        foreach (var (key, (segment, segmentPattern)) in TokenMap)
        {
            var escapedToken = Regex.Escape(key);

            regexPattern = regexPattern.Replace(
                escapedToken,
                $"(?<{segment:G}>{segmentPattern})");
        }



        regexPattern = @"^" + regexPattern + @"$";

        return new Regex(
            regexPattern,
            RegexOptions.Compiled,
            RegexTimeout
        );
    }
}

public interface ITrackEnricher
{
    Track Visitor(Track source, string path, string pattern);
}
