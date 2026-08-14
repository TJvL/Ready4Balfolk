using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The queue is judged on what reaches it and in what order, because stopping halfway through is
/// the normal case and what got answered by then is the whole product.
/// </summary>
public sealed class ReviewQueueTests
{
    private const string Root = "/music";

    private static readonly DateTime Written = new(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc);

    private readonly DanceListIndex _dances = DanceListIndex.Build(new DanceList
    {
        Dances = [TestData.CreateDance("mazurka", names: ["Mazurka"])]
    });

    [Fact]
    public void ATrackThatSaysNothingAtAll_IsInTheQueue()
    {
        // The 786 files a value-shaped queue could never reach: nothing is wrong with what they
        // claim, they claim nothing.
        var queue = Build([Entry("/music/a.mp3", [1], slug: null, originalDance: null, artist: null, title: "a")]);

        Assert.Single(Assert.Single(queue).Tracks);
    }

    [Fact]
    public void ATrackInTheLibrary_IsNotInTheQueue()
    {
        var queue = Build(
            [Entry("/music/a.mp3", [1])],
            Approved([1], TrackField.Dance, "Mazurka"),
            Approved([1], TrackField.Artist, "Naragonia"),
            Approved([1], TrackField.Title, "Le badaud"));

        Assert.Empty(queue);
    }

    [Fact]
    public void TheLeastConfidentComesFirst()
    {
        // Whoever gets through forty rows should have answered the forty nothing could speak for.
        var queue = Build(
        [
            Entry("/music/sure.mp3", [1]) with
            {
                Dance = new DerivedFrom(ClaimSourceKind.FileName, "brackets", DecisionReason.Corroborated),
                ArtistFrom = new DerivedFrom(ClaimSourceKind.Tag, "artist", DecisionReason.Corroborated),
                TitleFrom = new DerivedFrom(ClaimSourceKind.Tag, "title", DecisionReason.Corroborated)
            },
            Entry("/music/silent.mp3", [2], slug: null, originalDance: null)
        ]);

        Assert.Equal("silent.mp3", Assert.Single(queue).Tracks[0].FileName);
    }

    [Fact]
    public void TracksAreGroupedByWhereTheySit()
    {
        var queue = Build(
        [
            Entry("/music/Naragonia/a.mp3", [1]),
            Entry("/music/Naragonia/b.mp3", [2]),
            Entry("/music/TREF/c.mp3", [3])
        ]);

        Assert.Equal(2, queue.Count);
        Assert.Equal(2, queue.Single(group => group.Folder == "Naragonia").Tracks.Count);
    }

    [Fact]
    public void ALibraryWithNoFolders_IsOneFlatGroup()
    {
        var queue = Build([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])]);

        Assert.Equal(string.Empty, Assert.Single(queue).Folder);
    }

    [Fact]
    public void AGroupIsAsUnsureAsItsLeastSureTrack()
    {
        // So a folder holding one hopeless file is not buried behind folders of easy ones.
        var queue = Build(
        [
            Entry("/music/A/sure.mp3", [1]) with
            {
                Dance = new DerivedFrom(ClaimSourceKind.Tag, "album", DecisionReason.Corroborated),
                ArtistFrom = new DerivedFrom(ClaimSourceKind.Tag, "artist", DecisionReason.Corroborated),
                TitleFrom = new DerivedFrom(ClaimSourceKind.Tag, "title", DecisionReason.Corroborated)
            },
            Entry("/music/B/silent.mp3", [2], slug: null, originalDance: null, artist: null)
        ]);

        Assert.Equal("B", queue[0].Folder);
    }

    [Fact]
    public void AnApprovedTrackWhoseFileChanged_ComesBack()
    {
        // The screen is a fixture rather than a phase of setup: retag a file and it is back.
        var queue = Build(
            [Entry("/music/a.mp3", [1]) with { LastWriteUtc = Written.AddHours(1) }],
            Approved([1], TrackField.Dance, "Mazurka"),
            Approved([1], TrackField.Artist, "Naragonia"),
            Approved([1], TrackField.Title, "Le badaud"));

        var track = Assert.Single(Assert.Single(queue).Tracks);
        Assert.Equal(ReviewReason.ChangedSinceApproval, track.Review.Reason);
    }

    [Fact]
    public void AFieldCarriesWhereItCameFrom()
    {
        var queue = Build(
        [
            Entry("/music/a.mp3", [1]) with
            {
                ArtistFrom = new DerivedFrom(ClaimSourceKind.Folder, "level 1", DecisionReason.SoleValue)
            }
        ]);

        var track = Assert.Single(Assert.Single(queue).Tracks);
        Assert.Equal(ClaimSourceKind.Folder, track.Entry.ArtistFrom.Kind);
        Assert.Equal("level 1", track.Entry.ArtistFrom.Detail);
    }

    [Fact]
    public void TracksClaimingTheSameUnknownThing_KnowHowManyTheyAre()
    {
        // Answering "Scottiche" once has to be able to settle all of them.
        var queue = Build(
        [
            Entry("/music/a.mp3", [1], slug: null, originalDance: "Scottiche"),
            Entry("/music/b.mp3", [2], slug: null, originalDance: "scottiche"),
            Entry("/music/c.mp3", [3], slug: null, originalDance: "Rond de Landéda")
        ]);

        var tracks = queue.SelectMany(group => group.Tracks).ToList();
        Assert.Equal(2, tracks.Single(track => track.FileName == "a.mp3").SharedBy);
        Assert.Equal(1, tracks.Single(track => track.FileName == "c.mp3").SharedBy);
    }

    [Fact]
    public void AValueThatFoldsToNothing_SharesWithNothing()
    {
        // "???" folds to the empty string, and so does a row with no value at all. Grouping them
        // would hand one "use for all" click every unanswered row in the queue.
        var queue = Build(
        [
            Entry("/music/a.mp3", [1], slug: null, originalDance: "???"),
            Entry("/music/b.mp3", [2], slug: null, originalDance: "???"),
            Entry("/music/c.mp3", [3], slug: null, originalDance: null)
        ]);

        var tracks = queue.SelectMany(group => group.Tracks).ToList();
        Assert.Equal(0, tracks.Single(track => track.FileName == "a.mp3").SharedBy);
        Assert.Equal(0, tracks.Single(track => track.FileName == "c.mp3").SharedBy);
    }

    [Fact]
    public void ARecognisedDance_IsNotAnUnknownValue()
    {
        var queue = Build([Entry("/music/a.mp3", [1])]);

        Assert.Equal(string.Empty, Assert.Single(Assert.Single(queue).Tracks).UnknownValue);
    }

    [Fact]
    public void AValueSaidToBeJunk_IsShownAsNothingAtAll()
    {
        // Leaving "trad" in the box is leaving a wrong answer where somebody is looking for a
        // missing one, and it comes back off the file on every rescan.
        var queue = ReviewQueueBuilder.Build(
            new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
            {
                ["/music/a.mp3"] = Entry("/music/a.mp3", [1], slug: null, originalDance: "trad")
            },
            new Dictionary<string, IReadOnlyList<TrackApproval>>(StringComparer.Ordinal),
            _dances,
            Root,
            new HashSet<string>(StringComparer.Ordinal) { "trad" });

        var track = Assert.Single(Assert.Single(queue).Tracks);
        Assert.Null(track.Review.Dance.Value);
        Assert.Equal(ReviewReason.Missing, track.Review.Reason);
    }

    [Fact]
    public void AMisspelling_OffersWhatItProbablyMeant()
    {
        var queue = Build([Entry("/music/a.mp3", [1], slug: null, originalDance: "Mazurk")]);

        Assert.Equal(["Mazurka"], Assert.Single(Assert.Single(queue).Tracks).Suggestions);
    }

    private IReadOnlyList<ReviewGroup> Build(LibraryEntry[] entries, params TrackApproval[] approvals) =>
        ReviewQueueBuilder.Build(
            entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal),
            approvals
                .GroupBy(approval => LibraryKey.For(approval.ContentHash), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<TrackApproval>)[.. group], StringComparer.Ordinal),
            _dances,
            Root);

    private static TrackApproval Approved(byte[] hash, TrackField field, string value) => new()
    {
        ContentHash = hash,
        Field = field,
        Value = value,
        Kind = ApprovalKind.Individual,
        FileWriteUtc = Written
    };

    private static LibraryEntry Entry(
        string path,
        byte[] hash,
        string? slug = "mazurka",
        string? originalDance = "Mazurka",
        string? artist = "Naragonia",
        string? title = "Le badaud") => new()
        {
            ContentHash = hash,
            Path = path,
            FileSize = 1,
            LastWriteUtc = Written,
            Duration = TimeSpan.FromMinutes(3),
            Format = AudioFormat.Mp3,
            DanceSlug = slug,
            OriginalDance = originalDance,
            Artist = artist,
            Title = title
        };
}
