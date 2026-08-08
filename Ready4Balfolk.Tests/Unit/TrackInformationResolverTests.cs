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
        Categories =
        [
            TestData.CreateCategory("Common", dances:
            [
                TestData.CreateDance("mazurka", names: ["Mazurka", "Mazurk"]),
                TestData.CreateDance("scottish", names: ["Scottish", "Schottische"]),
                TestData.CreateDance("waltz", names: ["Valse", "Waltz"]),
                TestData.CreateDance("cercle-circassien", names: ["Cercle Circassien", "Cercle"]),
                TestData.CreateDance("bourree", names: ["Bourrée"]),
                TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
                TestData.CreateDance("andro", names: ["An dro", "Andro"])
            ])
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
    public void OldConvention_StillResolves()
    {
        // "Scottish - Bal O'Gadjo - Le badaud.mp3"
        var resolution = Resolve(Evidence("Scottish - Bal O'Gadjo - Le badaud", segments: ["Bal O'Gadjo"]));

        Assert.Equal("scottish", resolution.DanceSlug);
        Assert.Equal("Bal O'Gadjo", resolution.Artist);
        Assert.Equal("Le badaud", resolution.Title);
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
        var resolution = Resolve(Evidence("05 - Some Tune (Mazurka)") with { TagGenre = "Mazurka" });

        Assert.Equal("mazurka", resolution.DanceSlug);
        Assert.True(resolution.IsCorroborated);
        Assert.Contains(DanceEvidenceSource.FileName, resolution.AgreeingSources);
        Assert.Contains(DanceEvidenceSource.Tags, resolution.AgreeingSources);
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
        // Inventing a confident answer here is the failure this issue exists to prevent.
        var resolution = Resolve(Evidence("Some Tune (Mazurka)") with { TagGenre = "Scottish" });

        Assert.Null(resolution.DanceSlug);
    }

    [Fact]
    public void TwoDances_TheFilenamePatternBreaksTheTie()
    {
        var resolution = Resolve(Evidence("Mazurka - Someone - A Scottish Tune"));

        Assert.Equal("mazurka", resolution.DanceSlug);
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
    public void ArtistComesFromTheFolder()
    {
        var resolution = Resolve(Evidence("09. Bourree du 'tyot", segments: ["Tribal Jâze"]));

        Assert.Equal("Tribal Jâze", resolution.Artist);
    }

    [Fact]
    public void ArtistFolderBeatsARippersGuess()
    {
        var resolution = Resolve(
            Evidence("01 - Something", segments: ["Naragonia"]) with { TagArtist = "Unknown Artist" });

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
    public void TagArtist_IsUsedWhenThereIsNoFolder()
    {
        var resolution = Resolve(Evidence("01 - Something", segments: []) with { TagArtist = "Naragonia" });

        Assert.Equal("Naragonia", resolution.Artist);
    }

    [Fact]
    public void TrackNumberIsStrippedFromTheTitle()
    {
        Assert.Equal("Chavirage", Resolve(Evidence("09-Chavirage")).Title);
        Assert.Equal("Indifférence", Resolve(Evidence("04. Indifférence")).Title);
    }

    [Fact]
    public void OriginalDance_KeepsWhatTheFileClaimed_EvenWhenUnrecognised()
    {
        // 21 files claiming the same unknown thing have to group into one decision, so the value
        // has to survive not being recognised.
        var resolution = Resolve(Evidence("09-Scottiche à Leffondré"));

        Assert.Null(resolution.DanceSlug);
        Assert.Equal("Scottiche à Leffondré", resolution.Title);
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

    private TrackResolution Resolve(TrackEvidence evidence, string? folderDance = null)
        => TrackInformationResolver.Resolve(evidence, _index, folderDance);

    private static TrackEvidence Evidence(string fileName, IReadOnlyList<string>? segments = null) => new()
    {
        FileNameWithoutExtension = fileName,
        PathSegments = segments ?? ["Artist"],
        Duration = TimeSpan.FromSeconds(180),
        Format = AudioFormat.Mp3,
        ContentHash = [1]
    };
}
