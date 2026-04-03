using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

namespace Ready4Balfolk.Tests.Unit;

public sealed class TagSegmentDiscoveryTests
{
    [Fact]
    public void Return_Tagging_Test()
    {
        var mockFileSystem = new MockFileSystem();
        var fileInfo = mockFileSystem.FileInfo.New("TestFile.mp3");

        var audioFile = Substitute.For<IAudioFile>();
        audioFile.Title.Returns("Title");
        audioFile.Album.Returns("Album");
        audioFile.Artist.Returns("Artist");
        audioFile.Year.Returns(1234u);
        audioFile.Track.Returns(3u);
        audioFile.Genre.Returns("Genre");
        audioFile.Dance.Returns("Dance");

        var factory = Substitute.For<ITagFileFactory>();
        factory.Create(Arg.Any<IFileInfo>()).Returns(_ => audioFile);

        var tagSegmentDiscovery = new TagSegmentDiscovery(factory);
        var result = tagSegmentDiscovery.Scan(fileInfo);

        IEnumerable<KeyValuePair<PatternSegment, string>> expected =
        [
            new(PatternSegment.Album, "Album"),
            new(PatternSegment.Artist, "Artist"),
            new(PatternSegment.TrackNumber, "3"),
            new(PatternSegment.Year, "1234"),
            new(PatternSegment.Title, "Title"),
            new(PatternSegment.Genre, "Genre"),
            new(PatternSegment.Dance, "Dance"),
        ];

        Assert.Equal(expected, result);
    }
}
