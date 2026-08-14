using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// A track is in the library or in review and never both. These are the ways across and the ways it
/// is held back, each of which a person has to be able to see the reason for.
/// </summary>
public sealed class ReviewGateTests
{
    private static readonly DateTime Written = new(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc);

    private readonly DanceListIndex _dances = DanceListIndex.Build(new DanceList
    {
        Dances = [TestData.CreateDance("mazurka", names: ["Mazurka", "Mazurk"])]
    });

    [Fact]
    public void EverythingAnsweredAndApproved_IsInTheLibrary()
    {
        var review = Evaluate(Entry(), Approved(TrackField.Dance, "Mazurka"), Approved(TrackField.Artist, "Naragonia"), Approved(TrackField.Title, "Le badaud"));

        Assert.True(review.IsInLibrary);
        Assert.Equal("mazurka", review.DanceSlug);
    }

    [Fact]
    public void AFieldWithNoValueAtAll_HoldsTheTrackBack()
    {
        var review = Evaluate(Entry(artist: null), Approved(TrackField.Dance, "Mazurka"), Approved(TrackField.Title, "Le badaud"));

        Assert.Equal(ReviewReason.Missing, review.Reason);
    }

    [Fact]
    public void EverythingAnsweredAndNobodyAsked_IsNotInTheLibrary()
    {
        // Confidence is not approval. A value nothing has drawn attention to is exactly the one that
        // can be confidently wrong for years.
        var review = Evaluate(Entry());

        Assert.Equal(ReviewReason.Unapproved, review.Reason);
    }

    [Fact]
    public void OneFieldLeftUnapproved_StillHoldsTheTrackBack()
    {
        var review = Evaluate(Entry(), Approved(TrackField.Dance, "Mazurka"), Approved(TrackField.Artist, "Naragonia"));

        Assert.Equal(ReviewReason.Unapproved, review.Reason);
    }

    [Fact]
    public void ADanceTheListDoesNotKnow_ParksTheTrackEvenWhenARuleApprovedIt()
    {
        // Not a local problem to patch around: the value belongs in a proposal at BigBalfolkList,
        // and the track waits here until the list carries it.
        var review = Evaluate(
            Entry(),
            ByRule(TrackField.Dance, "Rond de Landéda"),
            Approved(TrackField.Artist, "Naragonia"),
            Approved(TrackField.Title, "Le badaud"));

        Assert.Equal(ReviewReason.UnknownDance, review.Reason);
        Assert.Null(review.DanceSlug);
        Assert.Equal("Rond de Landéda", review.Dance.Value);
    }

    [Fact]
    public void TheSameTrack_CrossesOnItsOwnWhenTheListLearnsTheName()
    {
        // A merged proposal has to visibly clear part of the backlog, and it does it without anybody
        // being asked a second time, because the approval was of the text rather than of a slug.
        var reimported = DanceListIndex.Build(new DanceList
        {
            Dances =
            [
                TestData.CreateDance("mazurka", names: ["Mazurka"]),
                TestData.CreateDance("rond-de-landeda", names: ["Rond de Landéda"])
            ]
        });

        var review = ReviewGate.Evaluate(
            Entry(),
            [
                ByRule(TrackField.Dance, "Rond de Landéda"),
                Approved(TrackField.Artist, "Naragonia"),
                Approved(TrackField.Title, "Le badaud")
            ],
            reimported);

        Assert.True(review.IsInLibrary);
        Assert.Equal("rond-de-landeda", review.DanceSlug);
    }

    [Fact]
    public void ASlugIsAsGoodAsAName()
    {
        // A rule approves whatever text it read; the editor approves the slug picked off the list.
        var review = Evaluate(Entry(), Approved(TrackField.Dance, "mazurka"), Approved(TrackField.Artist, "Naragonia"), Approved(TrackField.Title, "Le badaud"));

        Assert.True(review.IsInLibrary);
    }

    [Fact]
    public void AnApprovedValue_BeatsWhatWasDerived()
    {
        var review = Evaluate(Entry(artist: "Unknown Artist"), Approved(TrackField.Artist, "Naragonia"));

        Assert.Equal("Naragonia", review.Artist.Value);
        Assert.True(review.Artist.IsApproved);
    }

    [Fact]
    public void ADerivedValue_ShowsWithNoApprovalOnIt()
    {
        var review = Evaluate(Entry());

        Assert.Equal("Naragonia", review.Artist.Value);
        Assert.False(review.Artist.IsApproved);
    }

    [Fact]
    public void ARetagAfterApproval_BringsTheTrackBackWithItsValueIntact()
    {
        // The audio is the same, so the answer stands and is kept. Something changed under it
        // though, and that is worth eyes rather than a silent pass.
        var review = Evaluate(
            Entry() with { LastWriteUtc = Written.AddHours(1) },
            Approved(TrackField.Dance, "Mazurka"),
            Approved(TrackField.Artist, "Naragonia"),
            Approved(TrackField.Title, "Le badaud"));

        Assert.Equal(ReviewReason.ChangedSinceApproval, review.Reason);
        Assert.Equal("Naragonia", review.Artist.Value);
        Assert.True(review.Artist.IsApproved);
    }

    [Fact]
    public void AnUnrecognisedDanceValue_IsWhatTheFileSaid()
    {
        var review = Evaluate(Entry(slug: null, originalDance: "Scottiche à Leffondré"));

        Assert.Equal("Scottiche à Leffondré", review.Dance.Value);
    }

    [Fact]
    public void ARecognisedDance_ReadsAsTheListSpellsIt()
    {
        // So that four spellings of one dance group as one dance rather than as four.
        var review = Evaluate(Entry(slug: "mazurka", originalDance: "Mazurk"));

        Assert.Equal("Mazurka", review.Dance.Value);
    }

    [Fact]
    public void WhichRuleAnsweredAField_IsKeptForReviewToSay()
    {
        var review = Evaluate(Entry(), ByRule(TrackField.Artist, "Naragonia"));

        Assert.Equal("%d - %a - %t", review.Artist.Rule);
        Assert.Equal(ApprovalKind.ByRule, review.Artist.ApprovedAs);
    }

    private TrackReview Evaluate(LibraryEntry entry, params TrackApproval[] approvals) =>
        ReviewGate.Evaluate(entry, approvals, _dances);

    private static TrackApproval Approved(TrackField field, string value) => new()
    {
        ContentHash = [1],
        Field = field,
        Value = value,
        Kind = ApprovalKind.Individual,
        FileWriteUtc = Written
    };

    private static TrackApproval ByRule(TrackField field, string value) =>
        Approved(field, value) with { Kind = ApprovalKind.ByRule, Rule = "%d - %a - %t" };

    private static LibraryEntry Entry(
        string? slug = "mazurka",
        string? originalDance = "Mazurka",
        string? artist = "Naragonia",
        string? title = "Le badaud") => new()
        {
            ContentHash = [1],
            Path = "/music/a.mp3",
            FileSize = 1234,
            LastWriteUtc = Written,
            Duration = TimeSpan.FromMinutes(3),
            Format = AudioFormat.Mp3,
            DanceSlug = slug,
            OriginalDance = originalDance,
            Artist = artist,
            Title = title
        };
}
