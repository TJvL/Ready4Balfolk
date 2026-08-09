using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class ScanReportBuilderTests
{
    private readonly DanceListIndex _index = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("mazurka", names: ["Mazurka"]),
            TestData.CreateDance("scottish", names: ["Scottish"]),
            TestData.CreateDance("bourree-2-temps", names: ["Bourrée 2 temps"]),
            TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"])
        ]
    });

    [Fact]
    public void TwentyOneFilesClaimingTheSameThing_AreOneDecision()
    {
        var entries = Enumerable.Range(1, 21)
            .Select(i => Entry($"/music/Ar Re Yaouank/{i}.mp3", claimed: "Ar Re Yaouank"))
            .ToList();

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        var value = Assert.Single(report.Unrecognised);
        Assert.Equal("Ar Re Yaouank", value.Value);
        Assert.Equal(21, value.TrackCount);
    }

    [Fact]
    public void ValuesAreOrderedByHowMuchTheySettle()
    {
        var entries = new List<LibraryEntry>
        {
            Entry("/music/a/1.mp3", claimed: "Rare"),
            Entry("/music/b/1.mp3", claimed: "Common"),
            Entry("/music/b/2.mp3", claimed: "Common"),
            Entry("/music/b/3.mp3", claimed: "Common")
        };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        Assert.Equal("Common", report.Unrecognised[0].Value);
    }

    [Fact]
    public void AnIgnoredValue_DoesNotComeBack()
    {
        var entries = new List<LibraryEntry> { Entry("/music/a/1.mp3", claimed: "Folk") };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string> { "folk" });

        Assert.Empty(report.Unrecognised);
    }

    [Fact]
    public void SuggestionsAreRankedByWhatTheLibraryAlreadyPlays()
    {
        var entries = new List<LibraryEntry>
        {
            Resolved("/music/a/1.mp3", "bourree-3-temps"),
            Resolved("/music/a/2.mp3", "bourree-3-temps"),
            Resolved("/music/a/3.mp3", "bourree-2-temps"),
            Entry("/music/a/4.mp3", claimed: "Bourrée")
        };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        var value = Assert.Single(report.Unrecognised);
        Assert.Equal(UnrecognisedKind.TooGeneral, value.Kind);
        // The one this library actually plays comes first, not the alphabetically earlier one.
        Assert.Equal("bourree-3-temps", value.Suggestions[0].Slug);
    }

    [Fact]
    public void ATooGeneralValue_IsBrokenDownByFolderWhereTheEvidenceIs()
    {
        // A folder in which most tracks already read "Bourrée 3 temps" answers for the rest of it.
        var entries = new List<LibraryEntry>
        {
            Resolved("/music/Album A/1.mp3", "bourree-3-temps"),
            Resolved("/music/Album A/2.mp3", "bourree-3-temps"),
            Entry("/music/Album A/3.mp3", claimed: "Bourrée"),
            Entry("/music/Album B/1.mp3", claimed: "Bourrée")
        };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        var value = Assert.Single(report.Unrecognised);
        Assert.Equal(2, value.Folders.Count);

        var withEvidence = value.Folders[0];
        Assert.EndsWith("Album A", withEvidence.FolderKey, StringComparison.Ordinal);
        Assert.Equal("bourree-3-temps", withEvidence.Suggestions[0].Slug);

        // A folder that gives nothing stays per track rather than being guessed at.
        Assert.Empty(value.Folders[1].Suggestions);
    }

    [Fact]
    public void FilesWithNothingToSayAreCountedButNotListed()
    {
        var entries = new List<LibraryEntry> { Entry("/music/a/1.mp3", claimed: null) };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        Assert.Empty(report.Unrecognised);
        Assert.Equal(1, report.SilentlyUnresolved);
    }

    [Fact]
    public void ResolvedFilesAreCountedAsComplete()
    {
        var entries = new List<LibraryEntry>
        {
            Resolved("/music/a/1.mp3", "mazurka"),
            Entry("/music/a/2.mp3", claimed: "Folk")
        };

        var report = ScanReportBuilder.Build(entries, _index, ignoredValues: new HashSet<string>());

        Assert.Equal(1, report.Complete);
        Assert.Equal(1, report.UnrecognisedTrackCount);
    }

    [Fact]
    public void NothingToReport_IsSaidPlainly()
    {
        var report = ScanReportBuilder.Build(
            [Resolved("/music/a/1.mp3", "mazurka")], _index, ignoredValues: new HashSet<string>());

        Assert.False(report.HasAnythingToReport);
    }

    private static LibraryEntry Entry(string path, string? claimed) => new()
    {
        ContentHash = [(byte)path.Length],
        Path = path,
        FileSize = 1,
        LastWriteUtc = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
        Duration = TimeSpan.FromMinutes(3),
        Format = AudioFormat.Mp3,
        DanceSlug = null,
        OriginalDance = claimed
    };

    private static LibraryEntry Resolved(string path, string slug) => Entry(path, claimed: null) with
    {
        DanceSlug = slug
    };
}
