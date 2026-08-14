using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The filenames here are real ones from a balfolk library, because the thing this code has to
/// survive is not a convention but the absence of one.
/// </summary>
public sealed class TrackInformationResolverTests
{
    private readonly DanceListIndex _index = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("mazurka", names: ["Mazurka", "Mazurk"]),
            TestData.CreateDance("scottish", names: ["Scottish", "Schottische"]),
            TestData.CreateDance("waltz", names: ["Valse", "Waltz"]),
            TestData.CreateDance("cercle-circassien", names: ["Cercle Circassien", "Cercle"]),
            TestData.CreateDance("bourree", names: ["Bourrée"]),
            TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
            TestData.CreateDance("andro", names: ["An dro", "Andro"]),
            // A real dance whose name is also an ordinary French word, which is exactly the
            // collision that made "La fille du roi dans la tour" ambiguous.
            TestData.CreateDance("tour", names: ["Tour"])
        ]
    });

    [Fact]
    public void DanceInBrackets_IsFound()
    {
        // "10. Hep Harz (Cercle).mp3"
        var resolution = Resolve(Evidence("10. Hep Harz (Cercle)", segments: ["Plantec", "Awen (2012)"]));

        Assert.Equal("cercle-circassien", resolution.DanceSlug);
    }

    [Fact]
    public void DanceInBracketsLowercase_IsFound() =>
        // "12. Zero Step (mazurka).mp3"
        Assert.Equal("mazurka", Resolve(Evidence("12. Zero Step (mazurka)")).DanceSlug);

    [Fact]
    public void DanceAfterATrailingDash_IsFound() =>
        // "11-La Violette - valse 5tps.mp3"
        Assert.Equal("waltz", Resolve(Evidence("11-La Violette - valse 5tps")).DanceSlug);

    [Fact]
    public void AFileNameIsNotSplitIntoFields()
    {
        // "Scottish - Bal O'Gadjo - Le badaud.mp3". The dance is found because the name is in it,
        // not because it sits first. Nothing claims the second field is the artist: in the next
        // library along that same position is an album, a year or a track number.
        var resolution = Resolve(Evidence("Scottish - Bal O'Gadjo - Le badaud", segments: ["Bal O'Gadjo"]));

        Assert.Equal("scottish", resolution.DanceSlug);
        Assert.Equal(string.Empty, resolution.Artist);
        Assert.Equal("Scottish - Bal O'Gadjo - Le badaud", resolution.Title);
    }

    [Fact]
    public void BandNameInTheLeadingField_IsNotTakenForADance()
    {
        // The whole reason this rewrite exists: "An Tri dipop" is a band, not a dance, and the old
        // parser put it in the dance column for twenty files.
        var resolution = Resolve(Evidence("An Tri dipop - Ar Re Yaouank - Treizhour"));

        Assert.Null(resolution.DanceSlug);
    }

    [Fact]
    public void TrackNumberInTheLeadingField_IsNotTakenForADance() => Assert.Null(Resolve(Evidence("07 - Ar Re Yaouank - Tregor")).DanceSlug);

    [Fact]
    public void NoDanceAnywhere_ResolvesToNothing()
    {
        // "03-Track 3.mp3". Answering with nothing is the right answer.
        var resolution = Resolve(Evidence("03-Track 3", segments: ["TREF"]));

        Assert.Null(resolution.DanceSlug);
        Assert.False(resolution.IsResolved);
    }

    [Fact]
    public void FileNameAndTagsAgreeing_IsCorroborated()
    {
        var resolution = Resolve(Evidence("05 - Some Tune (Mazurka)") with { TagComment = "Mazurka" });

        var sources = resolution.DanceDecision.Chosen.Select(claim => claim.Source.Kind).ToList();

        Assert.Equal("mazurka", resolution.DanceSlug);
        Assert.True(resolution.IsCorroborated);
        Assert.Equal(DecisionReason.Corroborated, resolution.DanceDecision.Reason);
        Assert.Contains(ClaimSourceKind.FileName, sources);
        Assert.Contains(ClaimSourceKind.Tag, sources);
    }

    [Fact]
    public void OneSourceAlone_StillResolvesButIsNotCorroborated()
    {
        var resolution = Resolve(Evidence("05 - Some Tune (Mazurka)"));

        Assert.Equal("mazurka", resolution.DanceSlug);
        Assert.False(resolution.IsCorroborated);
    }

    [Fact]
    public void TwoDancesAndNothingToSeparateThem_ResolvesToNothing()
    {
        // Neither is bracketed and neither leads the filename, so there is genuinely nothing to
        // choose between them. Inventing a confident answer here is the failure this exists to
        // prevent.
        var resolution = Resolve(Evidence("Some Mazurka Tune") with { TagComment = "Scottish" });

        Assert.Null(resolution.DanceSlug);
    }

    [Fact]
    public void ADanceInBrackets_BeatsOneWrittenLooseInTheTags()
    {
        // Brackets are what a person wrote on purpose; a dance mentioned in a comment is not.
        var resolution = Resolve(Evidence("Some Tune (Mazurka)") with { TagComment = "Scottish" });

        Assert.Equal("mazurka", resolution.DanceSlug);
    }

    [Fact]
    public void TwoDances_TheLeadingFieldDoesNotBreakTheTie()
    {
        // The old pattern would have answered "mazurka" because it leads. Sitting first is a
        // position, not evidence, and "An Tri dipop - ..." is the same shape with a band in it.
        var resolution = Resolve(Evidence("Mazurka - Someone - A Scottish Tune"));

        Assert.Null(resolution.DanceSlug);
    }

    [Fact]
    public void LongerNameWins_OverTheOneInsideIt() => Assert.Equal("bourree-3-temps", Resolve(Evidence("04 - Bourrée 3 temps in G")).DanceSlug);

    [Fact]
    public void NameInsideALongerWord_DoesNotMatch() =>
        // "Andro" must not be found inside "Androgyne".
        Assert.Null(Resolve(Evidence("02 - Androgyne")).DanceSlug);

    [Fact]
    public void FolderAgreement_FillsAGapButIsNotInvented()
    {
        var evidence = Evidence("07 - Untitled");

        Assert.Null(Resolve(evidence).DanceSlug);
        Assert.Equal("mazurka", Resolve(evidence, folderDance: "mazurka").DanceSlug);
    }

    [Fact]
    public void FolderAgreement_DoesNotOverruleTheFile()
    {
        var resolution = Resolve(Evidence("07 - Some Tune (Scottish)"), folderDance: "mazurka");

        Assert.Equal("scottish", resolution.DanceSlug);
    }

    [Fact]
    public void AFolderIsNotTakenForTheArtist()
    {
        // The outermost folder is an artist in one library, a country in the next and a year in a
        // third. Until the user declares what a level means, it says nothing.
        var resolution = Resolve(Evidence("09. Bourree du 'tyot", segments: ["Tribal Jâze"]));

        Assert.Equal(string.Empty, resolution.Artist);
    }

    [Fact]
    public void AlbumArtistIsPreferredOverThePerformer()
    {
        var resolution = Resolve(Evidence("01 - Something")
            with
        { TagAlbumArtist = "Naragonia", TagArtist = "Toon Van Mierlo" });

        Assert.Equal("Naragonia", resolution.Artist);
    }

    [Theory]
    [InlineData("Unknown Artist")]
    [InlineData("Various Artists")]
    [InlineData("07")]
    [InlineData("   ")]
    public void RipperDefaults_AreNotBelievedAsArtists(string value)
    {
        var resolution = Resolve(Evidence("01 - Something", segments: []) with { TagArtist = value });

        Assert.Equal(string.Empty, resolution.Artist);
    }

    [Fact]
    public void ArtistComesFromTheTags()
    {
        var resolution = Resolve(Evidence("01 - Something") with { TagArtist = "Naragonia" });

        Assert.Equal("Naragonia", resolution.Artist);
    }

    [Fact]
    public void TrackNumberIsStrippedFromTheTitle()
    {
        // A leading number is not a name in any arrangement, so it comes off. Nothing else does.
        Assert.Equal("Chavirage", Resolve(Evidence("09-Chavirage")).Title);
        Assert.Equal("Indifférence", Resolve(Evidence("04. Indifférence")).Title);
    }

    [Fact]
    public void TheTitleTagIsPreferredOverTheFileName()
    {
        var resolution = Resolve(Evidence("07 - Track 07") with { TagTitle = "Le badaud" });

        Assert.Equal("Le badaud", resolution.Title);
    }

    [Fact]
    public void AnUnrecognisedName_IsNotClaimedAsADance()
    {
        // A misspelling nothing recognised is not a dance-shaped claim just because it leads the
        // file name: only brackets say a value was meant as the dance.
        var resolution = Resolve(Evidence("09-Scottiche à Leffondré"));

        Assert.Null(resolution.DanceSlug);
        Assert.Null(resolution.OriginalDance);
        Assert.Equal("Scottiche à Leffondré", resolution.Title);
    }

    [Fact]
    public void OriginalDance_IsNotTakenFromTheLeadingField()
    {
        // "An Tri dipop" is a band. Twenty files of it in the dance column is what this deletes.
        var resolution = Resolve(Evidence("An Tri dipop - Ar Re Yaouank - Treizhour"));

        Assert.Null(resolution.OriginalDance);
    }

    [Fact]
    public void OriginalDance_IsTheBracketedValueWhenItIsNotRecognised()
    {
        var resolution = Resolve(Evidence("05 - A Tune (Rond de Landéda)"));

        Assert.Null(resolution.DanceSlug);
        Assert.Equal("Rond de Landéda", resolution.OriginalDance);
    }

    [Fact]
    public void OriginalDance_IgnoresAYearInBrackets()
    {
        var resolution = Resolve(Evidence("05 - A Tune (1997)"));

        Assert.NotEqual("1997", resolution.OriginalDance);
    }

    [Fact]
    public void ADanceInBrackets_BeatsAnOrdinaryWordThatHappensToBeADance()
    {
        // Real case: "Tour" is a dance, and it collided with the word "tour" in the title. What
        // somebody wrote in brackets is a deliberate statement; a word in a sentence is not.
        var resolution = Resolve(Evidence("07-La fille du roi dans la tour (Mazurka)")
            with
        { TagTitle = "La fille du roi dans la tour (Mazurka)" });

        Assert.Equal("mazurka", resolution.DanceSlug);
    }

    [Fact]
    public void TwoDancesBothInBrackets_StillResolvesToNothing()
    {
        // "09. Thijsjes Doopwals (valse 3 temps, mazurka)" names two dances on purpose. Brackets
        // cannot separate them, so nothing is assumed.
        var resolution = Resolve(Evidence("09. Thijsjes Doopwals (valse, mazurka)"));

        Assert.Null(resolution.DanceSlug);
    }

    [Fact]
    public void TwoDancesAndNoBrackets_StillResolvesToNothing() =>
        // "03-ej lasko . mazurka_valse": genuinely both, and a person decides.
        Assert.Null(Resolve(Evidence("03-ej lasko . mazurka_valse")).DanceSlug);

    [Fact]
    public void ADeclaredValue_ReplacesEverythingObserved()
    {
        // The user stating the shape has taken responsibility for it, so the code stops hedging.
        // The tags are not argued with, they are simply not in the running.
        var resolution = Decide([
            Claim(TrackField.Artist, "Unknown Artist", ClaimSource.Tag("artist")),
            Claim(TrackField.Artist, "Naragonia", ClaimSource.FileName, ClaimTrust.Declared)
        ]);

        Assert.Equal("Naragonia", resolution.Artist);
        Assert.Equal(ClaimTrust.Declared, Assert.Single(resolution.ArtistDecision.Chosen).Trust);
    }

    [Fact]
    public void ADeclaredValue_IsNotCorroboratedByAWeakerOneAgreeing()
    {
        // A tier is not a vote. Two tiers agreeing is one tier deciding, and calling it corroborated
        // would be a stronger claim than anything actually made.
        var resolution = Decide([
            Claim(TrackField.Artist, "Naragonia", ClaimSource.Tag("artist")),
            Claim(TrackField.Artist, "Naragonia", ClaimSource.FileName, ClaimTrust.Declared)
        ]);

        Assert.Equal("Naragonia", resolution.Artist);
        Assert.Equal(DecisionReason.SoleValue, resolution.ArtistDecision.Reason);
    }

    [Fact]
    public void NothingSaid_AndNothingUsableSaid_AreDifferentAnswers()
    {
        // Both read as blank and they are not the same situation: one file needs a value invented,
        // the other needs a wrong one thrown away. A review screen has to be able to tell them apart.
        var silent = Decide([]);
        var useless = Decide([Claim(TrackField.Artist, "Various Artists", ClaimSource.Tag("artist"))]);

        Assert.Equal(DecisionReason.NoClaim, silent.ArtistDecision.Reason);
        Assert.Equal(DecisionReason.Unusable, useless.ArtistDecision.Reason);
    }

    [Fact]
    public void ADanceTheListDoesNotKnow_IsUnusableRatherThanSilence()
    {
        var resolution = Resolve(Evidence("05 - A Tune (Rond de Landéda)"));

        Assert.Equal(DecisionReason.Unusable, resolution.DanceDecision.Reason);
    }

    [Fact]
    public void TwoDancesWithNothingToSeparateThem_ReadAsContested()
    {
        var resolution = Resolve(Evidence("03-ej lasko . mazurka_valse"));

        Assert.Equal(DecisionReason.Contested, resolution.DanceDecision.Reason);
    }

    [Fact]
    public void TheClaimsThatLost_AreStillThere()
    {
        // Nothing is discarded silently. A wrong source is only visible next to what it beat.
        var resolution = Resolve(Evidence("Some Tune (Mazurka)") with { TagComment = "Scottish" });

        Assert.Equal("mazurka", resolution.DanceSlug);
        Assert.Contains(resolution.ClaimsFor(TrackField.Dance), claim => claim.Value == "scottish");
    }

    [Fact]
    public void ARefusedArtist_IsStillOnTheTrack()
    {
        var resolution = Resolve(Evidence("01 - Something") with { TagArtist = "Unknown Artist" });

        Assert.Equal(string.Empty, resolution.Artist);
        Assert.Contains(resolution.ClaimsFor(TrackField.Artist), claim => claim.Value == "Unknown Artist");
    }

    [Fact]
    public void TwoSourcesAgreeingOnAnArtist_IsCorroborated()
    {
        var resolution = Decide([
            Claim(TrackField.Artist, "Naragonia", ClaimSource.Tag("album artist")),
            Claim(TrackField.Artist, "naragonia", ClaimSource.FileName)
        ]);

        Assert.Equal(DecisionReason.Corroborated, resolution.ArtistDecision.Reason);
    }

    private static Claim Claim(
        TrackField field, string value, ClaimSource source, ClaimTrust trust = ClaimTrust.Observed) => new()
        {
            Field = field,
            Value = value,
            Source = source,
            Trust = trust
        };

    private TrackResolution Decide(IReadOnlyList<Claim> claims) => TrackInformationResolver.Decide(claims, _index);

    private TrackResolution Resolve(TrackEvidence evidence, string? folderDance = null)
        => TrackInformationResolver.Resolve(evidence, _index, folderDance: folderDance);

    private static TrackEvidence Evidence(string fileName, IReadOnlyList<string>? segments = null) => new()
    {
        FileName = fileName + ".mp3",
        PathSegments = segments ?? ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        ContentHash = [1]
    };
}
