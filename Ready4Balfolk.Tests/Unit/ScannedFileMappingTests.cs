using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>What a scan is turned into, without a scan or a database to reach it through.</summary>
/// <remarks>
/// The only other place this mapping runs is behind <c>TrackStoreTests</c>, against a substituted
/// index that a store test would have to think to look at. A wrong approval or a dropped field is
/// visible here directly, on the claims and decisions that produce it, rather than by chance.
/// </remarks>
public sealed class ScannedFileMappingTests
{
    private static readonly MockFileSystem FileSystem = new();

    private static ScannedFile CreateScannedFile(
        FieldDecision danceDecision, FieldDecision artistDecision, FieldDecision titleDecision,
        IReadOnlyList<Claim>? claims = null, string? originalDance = null)
    {
        FileSystem.AddFile("/music/salamandre.mp3", new MockFileData("id3"));
        var file = FileSystem.FileInfo.New("/music/salamandre.mp3");

        var evidence = new TrackEvidence
        {
            FileName = "salamandre.mp3",
            PathSegments = [],
            Duration = TimeSpan.FromMinutes(3),
            Format = AudioFormat.Mp3,
            ContentHash = [1, 2, 3]
        };

        var resolution = new TrackResolution
        {
            Claims = claims ?? [.. danceDecision.Chosen, .. artistDecision.Chosen, .. titleDecision.Chosen],
            DanceDecision = danceDecision,
            ArtistDecision = artistDecision,
            TitleDecision = titleDecision,
            OriginalDance = originalDance
        };

        return new ScannedFile(file, evidence, resolution);
    }

    private static FieldDecision NoClaim(TrackField field) =>
        new() { Field = field, Value = null, Reason = DecisionReason.NoClaim };

    // --- ByRuleApprovals ---

    [Fact]
    public void ByRuleApprovals_ADeclaredClaimTheListRecognises_IsApprovedWithTheDecidedValue()
    {
        // The declared rule spoke the raw text; the decision carries the slug the list resolved it
        // to. The approval has to carry the slug, not the words the rule matched on.
        var claim = new Claim
        {
            Field = TrackField.Dance,
            Value = "Mazurka",
            Source = ClaimSource.Pattern("dance"),
            Trust = ClaimTrust.Declared
        };
        var decision = new FieldDecision
        {
            Field = TrackField.Dance,
            Value = "mazurka",
            Reason = DecisionReason.SoleValue,
            Chosen = [claim]
        };

        var scanned = CreateScannedFile(decision, NoClaim(TrackField.Artist), NoClaim(TrackField.Title));

        var approval = Assert.Single(ScannedFileMapping.ByRuleApprovals(scanned));

        Assert.Equal(TrackField.Dance, approval.Field);
        Assert.Equal("mazurka", approval.Value);
        Assert.Equal(ApprovalKind.ByRule, approval.Kind);
        Assert.Equal("dance", approval.Rule);
        Assert.Equal(scanned.Evidence.ContentHash, approval.ContentHash);
        Assert.Equal(scanned.File.LastWriteTimeUtc, approval.FileWriteUtc);
    }

    [Fact]
    public void ByRuleApprovals_ADeclaredClaimTheListDoesNotRecognise_ParksOnTheClaimsOwnText()
    {
        // Nothing was chosen because the list does not know the word, so the decision itself has no
        // value to hand back. The rule still greenlit it, so it still approves, and what it approves
        // is the text the rule read rather than a slug that does not exist.
        var claim = new Claim
        {
            Field = TrackField.Dance,
            Value = "Andro",
            Source = ClaimSource.Pattern("dance"),
            Trust = ClaimTrust.Declared
        };
        var decision = new FieldDecision
        {
            Field = TrackField.Dance,
            Value = null,
            Reason = DecisionReason.Unusable,
            Chosen = []
        };

        var scanned = CreateScannedFile(
            decision, NoClaim(TrackField.Artist), NoClaim(TrackField.Title), claims: [claim]);

        var approval = Assert.Single(ScannedFileMapping.ByRuleApprovals(scanned));

        Assert.Equal(TrackField.Dance, approval.Field);
        Assert.Equal("Andro", approval.Value);
        Assert.Equal(ApprovalKind.ByRule, approval.Kind);
        Assert.Equal("dance", approval.Rule);
    }

    [Fact]
    public void ByRuleApprovals_AFieldNoDeclaredRuleAnswered_ProducesNoApproval()
    {
        // An observed claim alone, so a rule the DJ never greenlit is not credited with the answer.
        var claim = new Claim
        {
            Field = TrackField.Artist,
            Value = "Naragonia",
            Source = ClaimSource.Tag("artist"),
            Trust = ClaimTrust.Observed
        };
        var decision = new FieldDecision
        {
            Field = TrackField.Artist,
            Value = "Naragonia",
            Reason = DecisionReason.SoleValue,
            Chosen = [claim]
        };

        var scanned = CreateScannedFile(NoClaim(TrackField.Dance), decision, NoClaim(TrackField.Title));

        Assert.Empty(ScannedFileMapping.ByRuleApprovals(scanned));
    }

    // --- ToEntry ---

    [Fact]
    public void ToEntry_CarriesTheDerivedValuesAndWhatAnsweredThem()
    {
        var danceClaim = new Claim
        {
            Field = TrackField.Dance,
            Value = "Mazurka",
            Source = ClaimSource.Tag("comment"),
            Trust = ClaimTrust.Observed
        };
        var danceDecision = new FieldDecision
        {
            Field = TrackField.Dance,
            Value = "mazurka",
            Reason = DecisionReason.SoleValue,
            Chosen = [danceClaim]
        };

        var artistClaim = new Claim
        {
            Field = TrackField.Artist,
            Value = "Naragonia",
            Source = ClaimSource.Tag("artist"),
            Trust = ClaimTrust.Observed
        };
        var artistDecision = new FieldDecision
        {
            Field = TrackField.Artist,
            Value = "Naragonia",
            Reason = DecisionReason.SoleValue,
            Chosen = [artistClaim]
        };

        var titleDecision = NoClaim(TrackField.Title);

        var scanned = CreateScannedFile(danceDecision, artistDecision, titleDecision, originalDance: "Mazurka");

        var entry = ScannedFileMapping.ToEntry(scanned);

        Assert.Equal(scanned.Evidence.ContentHash, entry.ContentHash);
        Assert.Equal(scanned.File.FullName, entry.Path);
        Assert.Equal(scanned.File.Length, entry.FileSize);
        Assert.Equal(scanned.File.LastWriteTimeUtc, entry.LastWriteUtc);
        Assert.Equal(scanned.Evidence.Duration, entry.Duration);
        Assert.Equal(scanned.Evidence.Format, entry.Format);
        Assert.Equal("mazurka", entry.DanceSlug);
        Assert.Equal("Mazurka", entry.OriginalDance);
        Assert.Equal("Naragonia", entry.Artist);
        Assert.Equal(string.Empty, entry.Title);

        Assert.Equal(ClaimSourceKind.Tag, entry.Dance.Kind);
        Assert.Equal("comment", entry.Dance.Detail);
        Assert.Equal(DecisionReason.SoleValue, entry.Dance.Reason);

        Assert.Equal(ClaimSourceKind.Tag, entry.ArtistFrom.Kind);
        Assert.Equal("artist", entry.ArtistFrom.Detail);
        Assert.Equal(DecisionReason.SoleValue, entry.ArtistFrom.Reason);

        Assert.Equal(DerivedFrom.Nothing, entry.TitleFrom);
    }
}
