using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The preview is what makes a greenlight informed, so the numbers in it are the product. A count
/// that is off by a file is a user approving something other than what they were shown.
/// </summary>
public sealed class DeclarationPreviewTests
{
    private static readonly string[] Library =
    [
        "Scottish - Bal O'Gadjo - Le badaud.mp3",
        "Mazurka - Naragonia - Idiosyncrasie.mp3",
        "Valse - Trio Loubelya - La Sauvagine.mp3",
        "03-Track 3.mp3",
        "10. Hep Harz (Cercle).mp3"
    ];

    [Fact]
    public void APattern_SaysHowMuchOfTheLibraryItTakes()
    {
        var preview = DeclarationPreview.ForPattern("%d - %a - %t", Library);

        Assert.Equal(5, preview.Total);
        Assert.Equal(3, preview.Matched);
        Assert.Equal(2, preview.Missed);
    }

    [Fact]
    public void APattern_ShowsWhatItWouldMakeOfTheFilesItTakes()
    {
        var preview = DeclarationPreview.ForPattern("%d - %a - %t", Library);

        var first = preview.Matches[0];
        Assert.Equal("Scottish", first.Dance);
        Assert.Equal("Bal O'Gadjo", first.Artist);
        Assert.Equal("Le badaud", first.Title);
    }

    [Fact]
    public void APattern_ShowsThePileItWouldLeaveBehind()
    {
        // The misses are the queue somebody still has to work through, which is the other half of
        // knowing what a rule costs.
        var preview = DeclarationPreview.ForPattern("%d - %a - %t", Library);

        Assert.Contains("03-Track 3.mp3", preview.Misses);
        Assert.Contains("10. Hep Harz (Cercle).mp3", preview.Misses);
    }

    [Fact]
    public void Samples_AreCapped()
    {
        var many = Enumerable.Range(0, 100).Select(i => $"Artist - Title {i}.mp3").ToList();

        var preview = DeclarationPreview.ForPattern("%a - %t", many, sampleSize: 20);

        Assert.Equal(100, preview.Matched);
        Assert.Equal(20, preview.Matches.Count);
    }

    [Fact]
    public void ABadPattern_PreviewsAsAProblemRatherThanAsZeroMatches()
    {
        // "It matches nothing" and "it is not a pattern" would look identical otherwise, and only
        // one of them is the user's mistake to fix.
        var preview = DeclarationPreview.ForPattern("%a%t", Library);

        Assert.Equal(PatternProblem.AdjacentFields, preview.Problem);
        Assert.Equal(0, preview.Matched);
        Assert.Equal(5, preview.Total);
    }

    [Fact]
    public void APatternAskingForTheExtension_IsShownOne()
    {
        var preview = DeclarationPreview.ForPattern("%d - %a - %t.%ex", Library);

        Assert.Equal(3, preview.Matched);
    }

    [Fact]
    public void PatternsTogether_SayHowMuchIsStillUnaccountedFor()
    {
        // The number that matters after a rule is greenlit: what the next declaration is aimed at.
        var settings = new DiscoverySettings
        {
            UsesFileNamePatterns = true,
            FileNamePatterns = ["%d - %a - %t", "%n. %t"]
        };

        var coverage = DeclarationPreview.ForPatterns(settings, Library);

        Assert.Equal(4, coverage.Matched);
        Assert.Equal(1, coverage.Missed);
    }

    [Fact]
    public void TheLeftovers_AreTheFilesNoRuleTook()
    {
        var settings = new DiscoverySettings
        {
            UsesFileNamePatterns = true,
            FileNamePatterns = ["%d - %a - %t"]
        };

        var leftovers = DeclarationPreview.Leftovers(settings, Library);

        Assert.Equal(2, leftovers.Count);
        Assert.DoesNotContain("Scottish - Bal O'Gadjo - Le badaud.mp3", leftovers);
    }

    [Fact]
    public void NothingDeclared_LeavesEverything()
    {
        var leftovers = DeclarationPreview.Leftovers(DiscoverySettings.Undeclared, Library);

        Assert.Equal(Library.Length, leftovers.Count);
    }

    [Fact]
    public void AFolderLevel_ShowsWhatIsActuallyInIt()
    {
        IReadOnlyList<IReadOnlyList<string>> folders =
        [
            ["Naragonia", "Idiosyncrasie"],
            ["Naragonia", "Mazurka"],
            ["Bal O'Gadjo"],
            []
        ];

        var preview = DeclarationPreview.ForFolderLevel(1, folders);

        Assert.Equal(4, preview.Total);
        Assert.Equal(3, preview.FilesAtThisDepth);
        Assert.Equal(("Naragonia", 2), preview.Values[0]);
    }

    [Fact]
    public void AFolderLevelDeeperThanTheLibrary_IsHonestAboutIt()
    {
        IReadOnlyList<IReadOnlyList<string>> folders = [["Naragonia"], ["Bal O'Gadjo"]];

        var preview = DeclarationPreview.ForFolderLevel(3, folders);

        Assert.Equal(0, preview.FilesAtThisDepth);
        Assert.Empty(preview.Values);
    }
}
