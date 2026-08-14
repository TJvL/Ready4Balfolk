using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Matching;

namespace Ready4Balfolk.Tests.Unit;

public class PatternMatcherTests
{
    [Fact]
    public void EvaluateMatcherDefault()
    {
        var filename = Path.Combine("Dance - Artist - Title.mp3");
        var mockFileSystem = new MockFileSystem();

        mockFileSystem.AddFile(filename, new MockFileData(new byte[1]));

        // Get a mock IFileInfo from the mocked file system
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var track = PatternMatcher.Match(DiscoveryPattern.DefaultDefault, fileInfo);
        Assert.Multiple(
            () => Assert.Equal("Artist", track.GetValueOrDefault(PatternSegment.Artist)),
            () => Assert.Equal("Dance", track.GetValueOrDefault(PatternSegment.Dance)),
            () => Assert.Equal("Title", track.GetValueOrDefault(PatternSegment.Title))
        );
    }

    [Fact]
    public void EvaluateMatcherExtended()
    {
        var filename = Path.Combine("Rootfolder", "Test Musician", "Test Album", "01 - Test Track.mp3");
        var mockFileSystem = new MockFileSystem();

        mockFileSystem.AddFile(filename, new MockFileData(new byte[1]));

        // Get a mock IFileInfo from the mocked file system
        var fileInfo = mockFileSystem.FileInfo.New(filename);

        var track = PatternMatcher.Match(DiscoveryPattern.ExtendedDefault, fileInfo);
        Assert.Multiple(
            () => Assert.Equal("Test Musician", track.GetValueOrDefault(PatternSegment.Artist)),
            () => Assert.Equal("Test Album", track.GetValueOrDefault(PatternSegment.Album)),
            () => Assert.Equal("01", track.GetValueOrDefault(PatternSegment.TrackNumber)),
            () => Assert.Equal("Test Track", track.GetValueOrDefault(PatternSegment.Title))
        );
    }
}
