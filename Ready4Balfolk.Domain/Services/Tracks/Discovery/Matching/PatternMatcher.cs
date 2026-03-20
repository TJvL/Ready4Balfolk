using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using MoreLinq;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

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
