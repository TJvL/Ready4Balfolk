using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Exceptions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery;

namespace Ready4Balfolk.Tests.Unit;

public sealed class TrackInformationDiscoveryServiceTests
{
    private static IFileInfo CreateFileInfo() => new MockFileSystem().FileInfo.New("Song.mp3");

    [Fact]
    public void LoadMinimalSet_TagsWithoutDance_ConsultsLaterSteps()
    {
        // A file with ordinary Title/Artist tags must still get its dance from
        // a later step (dances.json / filename pattern).
        var tagStep = new FakeDiscovery(1,
            new(PatternSegment.Title, "Title"),
            new(PatternSegment.Artist, "Artist"));
        var danceStep = new FakeDiscovery(2,
            new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, "Mazurka"));
        var sut = new TrackInformationDiscoveryService(new OrderedSegmentDiscovery([tagStep, danceStep]));

        var segments = sut.LoadMinimalSet(CreateFileInfo());

        Assert.Equal("Mazurka", segments[PatternSegment.Dance]);
        Assert.Equal("Title", segments[PatternSegment.Title]);
        Assert.Equal("Artist", segments[PatternSegment.Artist]);
    }

    [Fact]
    public void LoadMinimalSet_AllSegmentsFound_SkipsRemainingSteps()
    {
        var completeStep = new FakeDiscovery(1,
            new(PatternSegment.Title, "Title"),
            new(PatternSegment.Artist, "Artist"),
            new(PatternSegment.Dance, "Mazurka"));
        var laterStep = new FakeDiscovery(2,
            new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, "Other"));
        var sut = new TrackInformationDiscoveryService(new OrderedSegmentDiscovery([completeStep, laterStep]));

        var segments = sut.LoadMinimalSet(CreateFileInfo());

        Assert.Equal("Mazurka", segments[PatternSegment.Dance]);
        Assert.Equal(0, laterStep.ScanCount);
    }

    [Fact]
    public void LoadMinimalSet_NoDanceAnywhere_StillReturnsMinimalSet()
    {
        var tagStep = new FakeDiscovery(1,
            new(PatternSegment.Title, "Title"),
            new(PatternSegment.Artist, "Artist"));
        var emptyStep = new FakeDiscovery(2);
        var sut = new TrackInformationDiscoveryService(new OrderedSegmentDiscovery([tagStep, emptyStep]));

        var segments = sut.LoadMinimalSet(CreateFileInfo());

        Assert.Equal(1, emptyStep.ScanCount);
        Assert.False(segments.ContainsKey(PatternSegment.Dance));
        Assert.Equal("Title", segments[PatternSegment.Title]);
    }

    [Fact]
    public void LoadMinimalSet_MinimalSetIncomplete_Throws()
    {
        var titleOnlyStep = new FakeDiscovery(1,
            new KeyValuePair<PatternSegment, string>(PatternSegment.Title, "Title"));
        var sut = new TrackInformationDiscoveryService(new OrderedSegmentDiscovery([titleOnlyStep]));

        Assert.Throws<TrackInformationDiscoveryException>(() => sut.LoadMinimalSet(CreateFileInfo()));
    }

    private sealed class FakeDiscovery(int order, params KeyValuePair<PatternSegment, string>[] segments)
        : IPatternSegmentDiscovery, IDiscoveryOrder
    {
        public int Order => order;

        public int ScanCount { get; private set; }

        public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo)
        {
            ScanCount++;
            return segments;
        }
    }
}
