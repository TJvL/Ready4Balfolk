using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The catalog's search box, which was a private method of TrackStore.</summary>
public sealed class TrackSearchFilterTests
{
    private static readonly MockFileSystem FileSystem = new();

    private static Track TrackWith(string dance, string artist, string title, string originalDance = "") =>
        new(dance, artist, title, FileSystem.FileInfo.New("/music/a.mp3"), TimeSpan.FromMinutes(3), AudioFormat.Mp3)
        {
            OriginalDance = originalDance
        };

    [Fact]
    public void For_AnEmptySearch_MatchesEverything()
    {
        var filter = TrackSearchFilter.For("");

        Assert.True(filter(TrackWith("Mazurka", "Naragonia", "Salamandre")));
    }

    [Theory]
    [InlineData("mazurka")]
    [InlineData("naragonia")]
    [InlineData("salamandre")]
    public void For_MatchesDanceArtistAndTitleAlike(string search)
    {
        var filter = TrackSearchFilter.For(search);

        Assert.True(filter(TrackWith("Mazurka", "Naragonia", "Salamandre")));
    }

    [Fact]
    public void For_IgnoresCaseAndAccents()
    {
        // Normalised on both sides, so what somebody types finds what the tag actually says.
        var filter = TrackSearchFilter.For("BOURREE");

        Assert.True(filter(TrackWith("Bourrée", "Someone", "Something")));
    }

    [Fact]
    public void For_MatchesTheDanceAsItWasOriginallyWritten()
    {
        // The published list may display a dance under a different name than the file carries, and
        // searching for what is on the file still has to find it.
        var filter = TrackSearchFilter.For("scottish");

        Assert.True(filter(TrackWith("Schottische", "Someone", "Something", originalDance: "Scottish")));
    }

    [Fact]
    public void For_NoFieldContainsIt_DoesNotMatch()
    {
        var filter = TrackSearchFilter.For("polka");

        Assert.False(filter(TrackWith("Mazurka", "Naragonia", "Salamandre")));
    }
}
