using System.IO.Abstractions.TestingHelpers;
using MoreLinq;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

namespace Ready4Balfolk.Tests.Unit;

public sealed class FilenameSegmentDiscoveryTests
{
    [Theory]
    [InlineData("test.mp3")]
    [InlineData("test")]
    public void Pattern_Matching_Empty_Pattern_Tests(string filename)
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var filenameSegmentDiscovery = new FilenameSegmentDiscovery(new DiscoveryPattern([]));

        var actual = filenameSegmentDiscovery.Scan(fileInfo);

        IEnumerable<KeyValuePair<PatternSegment, string>> expected = [];

        Assert.Equal(expected, actual);
    }

    public static IEnumerable<TheoryDataRow<string, string?, PatternSegment, string>> SinglePatternStrings()
    {
        /*
           { "%a", ("artist", PatternSegment.Artist, @".+?") },
           { "%l", ("album", PatternSegment.Album, @".+?") },
           { "%t", ("title", PatternSegment.Title, @".+?") },
           { "%n", ("tracknumber", PatternSegment.TrackNumber, @"\d+") },
           { "%y", ("year", PatternSegment.Year, @"\d{4}") },
           { "%g", ("genre", PatternSegment.Genre, @".+?") },

           { "%d", ("dance", PatternSegment.Dance, @".+?") },
           { "%x", ("extension", null, @"\w{3}") }
         */

        ICollection<(string pattern, PatternSegment Segment)> stringTags = [
            ("%a", PatternSegment.Artist),
            ("%l", PatternSegment.Album),
            ("%t", PatternSegment.Title),
            ("%g", PatternSegment.Genre),
            ("%d", PatternSegment.Dance),
        ];

        ICollection<string> filenames = ["test", "with spaces", "with - dashes - here"];

        return filenames.Cartesian(stringTags, (filename, tag) => new TheoryDataRow<string, string?, PatternSegment, string>($"{filename}.mp3", filename, tag.Segment, $"{tag.pattern}.%x"));
    }

    [Theory]
    [MemberData(nameof(SinglePatternStrings))]
    public void Pattern_Matching_Single_Text_Pattern_Tests(string filename, string? segmentValue, PatternSegment segment, string pattern)
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var filenameSegmentDiscovery = new FilenameSegmentDiscovery(new DiscoveryPattern([pattern]));

        var actual = filenameSegmentDiscovery.Scan(fileInfo);

        List<KeyValuePair<PatternSegment, string>> expected = [];
        if (segmentValue is not null)
        {
            expected.Add(new KeyValuePair<PatternSegment, string>(segment, segmentValue));
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.mp3", null, "%y.%x")]
    [InlineData("12.mp3", null, "%y.%x")]
    [InlineData("123.mp3", null, "%y.%x")]
    [InlineData("1234.mp3", "1234", "%y.%x")]
    [InlineData("4455", "4455", "%y")]
    [InlineData("dance - 2233 - cool.mp3", "2233", "%d - %y - %a.%x")]
    [InlineData("dance - 223 - cool.mp3", null, "%d - %y - %a.%x")]
    public void Pattern_Matching_Year_Pattern_Tests(string filename, string? segmentValue, string pattern)
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var filenameSegmentDiscovery = new FilenameSegmentDiscovery(new DiscoveryPattern([pattern]));

        var actual = filenameSegmentDiscovery.Scan(fileInfo);

        if (segmentValue is not null)
        {
            var trackNumber = Assert.Single(actual, s => s.Key == PatternSegment.Year);
            Assert.Equal(segmentValue, trackNumber.Value);
        }
        else
        {
            Assert.DoesNotContain(actual, s => s.Key == PatternSegment.TrackNumber);
        }
    }

    [Theory]
    [InlineData("artist.mp3", true, "%a.%x")]
    [InlineData("artist.mp4", true, "%a.%x")]
    [InlineData("artist.ogg", true, "%a.%x")]
    [InlineData("artist.nope", false, "%a.%x")]
    public void Pattern_Matching_Extension_Tests(string filename, bool match, string pattern)
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var filenameSegmentDiscovery = new FilenameSegmentDiscovery(new DiscoveryPattern([pattern]));

        var actual = filenameSegmentDiscovery.Scan(fileInfo);

        Assert.Equal(match, actual.Any());
    }

    [Theory]
    [InlineData("1.mp3", "1", "%n.%x")]
    [InlineData("12.mp3", "12", "%n.%x")]
    [InlineData("123.mp3", "123", "%n.%x")]
    [InlineData("123", "123", "%n")]
    [InlineData("dance - 123 - cool.mp3", "123", "%d - %n - %a.%x")]
    public void Pattern_Matching_Track_Pattern_Tests(string filename, string segmentValue, string pattern)
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var filenameSegmentDiscovery = new FilenameSegmentDiscovery(new DiscoveryPattern([pattern]));

        var actual = filenameSegmentDiscovery.Scan(fileInfo);

        var trackNumber = Assert.Single(actual, s => s.Key == PatternSegment.TrackNumber);
        var expected = new KeyValuePair<PatternSegment, string>(PatternSegment.TrackNumber, segmentValue);
        Assert.Equal(expected, trackNumber);
    }
}
