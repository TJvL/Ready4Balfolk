using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
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
                with
        { TagArtist = "Toon Van Mierlo", TagAlbumArtist = "Naragonia" })
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

    [Fact]
    public void ADeclaredPattern_ClaimsWhatItPicksOut()
    {
        var claims = Collect(
            Evidence("Scottish - Bal O'Gadjo - Le badaud"),
            Declared(patterns: ["%d - %a - %t"]));

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Value == "Scottish" && claim.Trust == ClaimTrust.Declared);
        Assert.Contains(claims, claim => claim.Field == TrackField.Artist && claim.Value == "Bal O'Gadjo" && claim.Trust == ClaimTrust.Declared);
        Assert.Contains(claims, claim => claim.Field == TrackField.Title && claim.Value == "Le badaud" && claim.Trust == ClaimTrust.Declared);
    }

    [Fact]
    public void ASwitchedOffMechanism_SaysNothingAtAll()
    {
        // Everything is filled in and every switch is off, which is what a person who tried the
        // patterns and settled on their folders leaves behind. What they cannot see cannot speak.
        var switchedOff = DeclaredDiscovery.Compile(new DiscoverySettings
        {
            FileNamePatterns = ["%d - %a - %t"],
            FolderRoles = [FolderRole.Artist],
            TagTrust = new TagTrust { Dance = [TagField.Comment] },
            CustomDanceTag = "DANCE"
        });

        var claims = Collect(
            Evidence("Scottish - Bal O'Gadjo - Le badaud", ["Naragonia"]) with
            {
                TagComment = "Mazurka",
                CustomTags = Tags(("DANCE", "Bourrée"))
            },
            switchedOff);

        Assert.DoesNotContain(claims, claim => claim.Trust == ClaimTrust.Declared);
        Assert.DoesNotContain(claims, claim => claim.Value == "Bourrée");
    }

    [Fact]
    public void SwitchedOffTagTrust_LeavesTheBuiltInGuesses()
    {
        // Off is the undeclared library, not a library with no tags: the artist and the title are
        // still read off the tags, and still claimed as the guesses they are.
        var switchedOff = DeclaredDiscovery.Compile(new DiscoverySettings
        {
            TagTrust = new TagTrust { Artist = [], Title = [] }
        });

        var claims = Collect(
            Evidence("01 - Something") with { TagArtist = "Naragonia", TagTitle = "Salamandre" },
            switchedOff);

        Assert.Contains(
            claims,
            claim => claim.Field == TrackField.Artist
                     && claim.Value == "Naragonia"
                     && claim.Trust == ClaimTrust.Observed);
    }

    [Fact]
    public void OnlyTheFirstPatternToMatch_Speaks()
    {
        // Order is the user's way of saying which of two overlapping shapes their library means.
        var claims = Collect(
            Evidence("Naragonia - Mazurka"),
            Declared(patterns: ["%a - %t", "%d - %t"]));

        Assert.Contains(claims, claim => claim.Field == TrackField.Artist && claim.Value == "Naragonia");
        Assert.DoesNotContain(claims, claim => claim.Trust == ClaimTrust.Declared && claim.Field == TrackField.Dance);
    }

    [Fact]
    public void APatternThatDoesNotMatch_ClaimsNothing() =>
        Assert.DoesNotContain(
            Collect(Evidence("03-Track 3"), Declared(patterns: ["%d - %a - %t"])),
            claim => claim.Trust == ClaimTrust.Declared);

    [Fact]
    public void TheObservedReadingSurvivesADeclaration()
    {
        // The tags are not argued with, they are outranked, and they stay on the track so a person
        // can see that the rule disagreed with them.
        var claims = Collect(
            Evidence("Naragonia - Mazurka") with { TagArtist = "Toon Van Mierlo" },
            Declared(patterns: ["%a - %t"]));

        Assert.Contains(claims, claim => claim.Value == "Toon Van Mierlo" && claim.Trust == ClaimTrust.Observed);
    }

    [Fact]
    public void ADeclaredFolderRole_ClaimsTheFolderName()
    {
        var claims = Collect(
            Evidence("01 - Something", segments: ["Naragonia", "Idiosyncrasie"]),
            Declared(roles: [FolderRole.Artist, FolderRole.Album]));

        var artist = Assert.Single(claims, claim => claim.Field == TrackField.Artist);
        Assert.Equal("Naragonia", artist.Value);
        Assert.Equal(ClaimTrust.Declared, artist.Trust);
        Assert.Equal("level 1", artist.Source.Detail);

        // An album level is worth declaring and there is nothing on a track to claim from it.
        Assert.DoesNotContain(claims, claim => claim.Value == "Idiosyncrasie");
    }

    [Fact]
    public void AFolderRole_IsAppliedOnlyWhereTheDepthIsThere()
    {
        // Three levels in one corner of a library and one in another is ordinary.
        var claims = Collect(Evidence("01 - Something", segments: []), Declared(roles: [FolderRole.Artist]));

        Assert.DoesNotContain(claims, claim => claim.Field == TrackField.Artist);
    }

    [Fact]
    public void ADeclaredDanceFolder_IsClaimedWhetherOrNotTheListKnowsIt()
    {
        var claims = Collect(Evidence("01 - Something", segments: ["Rond de Landéda"]), Declared(roles: [FolderRole.Dance]));

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Value == "Rond de Landéda" && claim.Trust == ClaimTrust.Declared);
    }

    [Fact]
    public void ADeclaredTagField_IsReadWhole()
    {
        // The difference between trusting a field and finding a name in it: a declared field is the
        // dance even when the list has never heard of it, which is what parks the track.
        var claims = Collect(
            Evidence("01 - Something") with { TagComment = "Rond de Landéda" },
            Declared(trust: new TagTrust { Dance = [TagField.Comment] }));

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Value == "Rond de Landéda" && claim.Trust == ClaimTrust.Declared);
    }

    [Fact]
    public void ADeclaredTagOrder_IsADeclaration()
    {
        var claims = Collect(
            Evidence("01 - Something") with { TagArtist = "Naragonia" },
            Declared(trust: new TagTrust { Artist = [TagField.Artist] }));

        Assert.Contains(claims, claim => claim.Field == TrackField.Artist && claim.Trust == ClaimTrust.Declared);
    }

    [Fact]
    public void TheDefaultTagOrder_IsAGuessAndIsClaimedAsOne() =>
        Assert.All(
            Collect(Evidence("01 - Something") with { TagArtist = "Naragonia" })
                .Where(claim => claim.Field == TrackField.Artist),
            claim => Assert.Equal(ClaimTrust.Observed, claim.Trust));

    [Fact]
    public void ADeclaredTagList_CanSayThatNothingSpeaks() =>
        Assert.DoesNotContain(
            Collect(
                Evidence("01 - Something") with { TagArtist = "Naragonia" },
                Declared(trust: new TagTrust { Artist = [] })),
            claim => claim.Field == TrackField.Artist);

    [Fact]
    public void ANameFromTheListInATag_NeedsNoDeclaration()
    {
        // The vocabulary recognising itself is not a guess about what a field means, so it is not
        // governed by tag trust. It stays observed, and it stays.
        var claims = Collect(Evidence("01 - Something") with { TagAlbum = "Mazurka" });

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Value == "mazurka");
    }

    [Fact]
    public void ABracketedNameWithGlueWords_IsStillDeliberate()
    {
        // The scanner's matched names are match keys, glue dropped; the bracket text has to be
        // folded the same way or "(Rond de Saint-Vincent)" never reads as written-on-purpose.
        var index = DanceListIndex.Build(new DanceList
        {
            IgnoredWords = ["de"],
            Dances = [TestData.CreateDance("rond-de-saint-vincent", names: ["Rond de Saint-Vincent"])]
        });

        var claims = TrackClaims.Collect(Evidence("Some tune (Rond de Saint-Vincent)"), index);

        Assert.Contains(claims, claim =>
            claim.Field == TrackField.Dance && claim.Source.IsDeliberate);
    }

    [Fact]
    public void ABracketedNameWithNumberWords_IsStillDeliberate()
    {
        var index = DanceListIndex.Build(new DanceList
        {
            NumberWords = new Dictionary<string, string> { ["trois"] = "3" },
            Dances = [TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"])]
        });

        var claims = TrackClaims.Collect(Evidence("05 - A tune (Bourrée à trois temps)"), index);

        Assert.Contains(claims, claim =>
            claim.Field == TrackField.Dance && claim.Source.IsDeliberate);
    }

    [Fact]
    public void ADeclaredCustomTag_IsReadWhole()
    {
        // Named by the user, so it is a declaration like a trusted field: the value is the dance
        // even when the list has never heard of it, which is what parks the track.
        var claims = Collect(
            Evidence("01 - Something") with { CustomTags = Tags(("DANCE", "Rond de Landéda")) },
            Declared(customDanceTag: "DANCE"));

        Assert.Contains(claims, claim =>
            claim.Field == TrackField.Dance
            && claim.Value == "Rond de Landéda"
            && claim.Trust == ClaimTrust.Declared
            && claim.Source.Kind == ClaimSourceKind.Tag
            && claim.Source.Detail == "DANCE");
    }

    [Fact]
    public void ACustomTagName_IsMatchedCaseInsensitively()
    {
        var claims = Collect(
            Evidence("01 - Something") with { CustomTags = Tags(("dance", "Mazurka")) },
            Declared(customDanceTag: "DANCE"));

        Assert.Contains(claims, claim => claim.Field == TrackField.Dance && claim.Trust == ClaimTrust.Declared);
    }

    [Fact]
    public void AnUndeclaredCustomTag_SaysNothing() =>
        // The tag sits in the file either way; without the user naming it, no field of the track
        // is read from it, because what a free-form tag means is not a thing a library can be asked.
        Assert.DoesNotContain(
            Collect(Evidence("01 - Something") with { CustomTags = Tags(("DANCE", "Rond de Landéda")) }),
            claim => claim.Field == TrackField.Dance);

    [Fact]
    public void ADeclaredCustomTagTheFileDoesNotCarry_ClaimsNothing() =>
        Assert.DoesNotContain(
            Collect(Evidence("01 - Something"), Declared(customDanceTag: "DANCE")),
            claim => claim.Field == TrackField.Dance);

    private static Dictionary<string, string> Tags(params (string Name, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

    private static DeclaredDiscovery Declared(
        IReadOnlyList<string>? patterns = null,
        IReadOnlyList<FolderRole>? roles = null,
        TagTrust? trust = null,
        string? customDanceTag = null) =>
        DeclaredDiscovery.Compile(new DiscoverySettings
        {
            // A mechanism is only read when it is switched on, and stating something through this
            // helper is a test saying the user switched that one on.
            UsesFileNamePatterns = patterns is not null,
            UsesFolderRoles = roles is not null,
            UsesTagTrust = trust is not null,
            UsesCustomDanceTag = customDanceTag is not null,
            FileNamePatterns = patterns ?? [],
            FolderRoles = roles ?? [],
            TagTrust = trust ?? new TagTrust(),
            CustomDanceTag = customDanceTag
        });

    private IReadOnlyList<Claim> Collect(
        TrackEvidence evidence, DeclaredDiscovery? declared = null, string? folderDance = null) =>
        TrackClaims.Collect(evidence, _index, declared, folderDance);

    private static TrackEvidence Evidence(string fileName, IReadOnlyList<string>? segments = null) => new()
    {
        FileName = fileName + ".mp3",
        PathSegments = segments ?? ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        ContentHash = [1]
    };
}
