using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Collecting is judged on what it keeps, not on what it answers. A claim that is dropped here can
/// never be shown to anyone, so the test for "nothing is discarded silently" lives at this level.
/// </summary>
public sealed class TrackClaimsTests
{
    private readonly DanceListIndex _index = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("mazurka", names: ["Mazurka", "Mazurk"]),
            TestData.CreateDance("scottish", names: ["Scottish", "Schottische"]),
            TestData.CreateDance("waltz", names: ["Valse", "Waltz"])
        ]
    });

    [Fact]
    public void EveryFieldOfEveryClaim_SaysWhatItIsAndWhereItCameFrom()
    {
        var claims = Collect(Evidence("05 - Some Tune (Mazurka)") with { TagArtist = "Naragonia" });

        Assert.All(claims, claim =>
        {
            Assert.NotEmpty(claim.Value);
            Assert.NotEmpty(claim.Source.Detail);
            Assert.Equal(ClaimTrust.Observed, claim.Trust);
        });
    }

    [Fact]
    public void ATagAndAFileName_AreSeparateSources()
    {
        var claims = Collect(Evidence("Some Tune (Mazurka)") with { TagComment = "Scottish" });

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Source.Kind == ClaimSourceKind.FileName);
        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Source.Kind == ClaimSourceKind.Tag);
    }

    [Fact]
    public void EachTagFieldIsNamed_RatherThanReadAsOneBlob()
    {
        // Which field a dance was written into is a thing the user will want to declare over, and a
        // blob of every tag joined together cannot be declared over.
        var claims = Collect(Evidence("Some Tune") with { TagAlbum = "Mazurka", TagComment = "Scottish" });

        Assert.Contains(claims, claim => claim.Value == "mazurka" && claim.Source.Detail == "album");
        Assert.Contains(claims, claim => claim.Value == "scottish" && claim.Source.Detail == "comment");
    }

    [Fact]
    public void ADanceInBrackets_IsClaimedAsDeliberate()
    {
        var claims = Collect(Evidence("La fille du roi (Mazurka)"));

        Assert.Contains(claims, claim => claim.Value == "mazurka" && claim.Source.IsDeliberate);
    }

    [Fact]
    public void ADanceLooseInTheName_IsNotDeliberate() =>
        Assert.All(
            Collect(Evidence("A Mazurka Tune")).Where(claim => claim.Field == TrackField.Dance),
            claim => Assert.False(claim.Source.IsDeliberate));

    [Fact]
    public void AnUnrecognisedBracketedValue_IsStillAClaim()
    {
        // The list not knowing it is the whole reason it has to survive: it is what a person maps,
        // or opens a proposal for, and it is what parks the track until they do.
        var claims = Collect(Evidence("05 - A Tune (Rond de Landéda)"));

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Value == "Rond de Landéda");
    }

    [Fact]
    public void AYearInBrackets_IsNotAClaimAboutTheDance() =>
        Assert.DoesNotContain(
            Collect(Evidence("05 - A Tune (1997)")),
            claim => claim.Field == TrackField.Dance);

    [Fact]
    public void APlaceholderArtist_IsClaimedAndRefusedLater()
    {
        // Refusing it while deciding is right; dropping it here is not. "The artist tag says Unknown
        // Artist" and "the file has no artist tag" are different things to be looking at.
        var claims = Collect(Evidence("01 - Something") with { TagArtist = "Unknown Artist" });

        Assert.Contains(claims, claim => claim.Field == TrackField.Artist && claim.Value == "Unknown Artist");
    }

    [Fact]
    public void ArtistClaimsComeOutInTheOrderTheyAreTrusted()
    {
        var artists = Collect(Evidence("01 - Something")
                with { TagArtist = "Toon Van Mierlo", TagAlbumArtist = "Naragonia" })
            .Where(claim => claim.Field == TrackField.Artist)
            .ToList();

        Assert.Equal(["album artist", "artist"], artists.Select(claim => claim.Source.Detail));
    }

    [Fact]
    public void AFolderName_ClaimsNothing()
    {
        // Level 1 is an artist in one library, a country in the next and a year in a third. Until
        // the user says which, a folder name is not a claim about anything.
        var claims = Collect(Evidence("01 - Something", segments: ["Naragonia", "Idiosyncrasie (2011)"]));

        Assert.DoesNotContain(claims, claim => claim.Value.Contains("Naragonia", StringComparison.Ordinal));
        Assert.DoesNotContain(claims, claim => claim.Value.Contains("Idiosyncrasie", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFolderAgreement_IsItsOwnSource()
    {
        var claims = Collect(Evidence("07 - Untitled"), folderDance: "mazurka");

        var folder = Assert.Single(claims, claim => claim.Source.Kind == ClaimSourceKind.Folder);
        Assert.Equal(TrackField.Dance, folder.Field);
        Assert.Equal("Mazurka", folder.Value);
    }

    [Fact]
    public void TheFileNameIsClaimedWholeAsATitle()
    {
        // Only the track number comes off. Which part of the rest is the title is exactly what an
        // undeclared library cannot be asked.
        var claims = Collect(Evidence("07 - Bal O'Gadjo - Le badaud"));

        Assert.Contains(
            claims,
            claim => claim.Field == TrackField.Title && claim.Value == "Bal O'Gadjo - Le badaud");
    }

    private IReadOnlyList<Claim> Collect(TrackEvidence evidence, string? folderDance = null) =>
        TrackClaims.Collect(evidence, _index, folderDance);

    private static TrackEvidence Evidence(string fileName, IReadOnlyList<string>? segments = null) => new()
    {
        FileNameWithoutExtension = fileName,
        PathSegments = segments ?? ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        ContentHash = [1]
    };
}
