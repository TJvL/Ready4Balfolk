using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Grammar is not a spelling. One bourrée written five ways has to land on one dance, or the list
/// grows a dozen names for it and a library written in French matches nothing.
/// </summary>
public sealed class DanceWordsTests
{
    private static readonly DanceList List = new()
    {
        IgnoredWords = ["a", "de", "la", "le", "les", "in", "temps", "times", "tijden", "van"],
        NumberWords = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trois"] = "3",
            ["drie"] = "3",
            ["drei"] = "3",
            ["three"] = "3",
            ["3t"] = "3",
            ["deux"] = "2",
            ["twee"] = "2",
            ["2t"] = "2"
        },
        Tags = ["france"],
        Dances =
        [
            TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
            TestData.CreateDance("mazurka", names: ["Mazurka"]),
            TestData.CreateDance("valse", names: ["Valse"])
        ]
    };

    private static readonly DanceListIndex Index = DanceListIndex.Build(List);

    [Theory]
    [InlineData("Bourrée à 3 temps")]
    [InlineData("Bourrée in 3")]
    [InlineData("Bourrée à trois temps")]
    [InlineData("Bourree 3t")]
    [InlineData("Bourrée 3")]
    [InlineData("Bourree drie tijden")]
    public void OneNameWrittenSeveralWays_IsOneDance(string written) =>
        Assert.Equal("bourree-3-temps", Index.ResolveSlug(written));

    [Fact]
    public void ANameThatIsAllGlue_KeepsItsFoldedForm()
    {
        // Otherwise every such name keys to nothing and they are all the same name.
        var words = DanceWords.From(List);

        Assert.Equal("in de", words.KeyFor("In de"));
    }

    [Fact]
    public void NumbersAreOnlyMappedAsWholeWords()
    {
        var words = DanceWords.From(List);

        // "Trois" is a number word; "Troisième" is a word that starts like one.
        Assert.Equal("3", words.KeyFor("trois"));
        Assert.Equal("troisieme", words.KeyFor("Troisième"));
    }

    [Fact]
    public void AFileNameWrittenInAnotherLanguage_IsStillFound()
    {
        // "09 - Bourree in drie tijden.mp3": nothing in this file is spelled the way the list is.
        var found = DanceNameScanner.Scan("09 - Bourree in drie tijden", Index);

        Assert.Equal("bourree-3-temps", Assert.Single(found).Slug);
    }

    [Fact]
    public void GlueIsSteppedOverRatherThanEndingAMatch()
    {
        var found = DanceNameScanner.Scan("Bourrée à trois temps (live)", Index);

        Assert.Equal("bourree-3-temps", Assert.Single(found).Slug);
    }

    [Fact]
    public void AWordThatIsNotGlue_StillEndsAMatch() =>
        // "Bourrée du Berry 3 temps" is not the dance "Bourrée 3 temps": something real sits
        // between the words, and stepping over that would match anything that shares two words.
        Assert.Empty(DanceNameScanner.Scan("Bourrée du Berry 3 temps", Index));

    [Fact]
    public void TwoDancesInOneName_AreStillTwo()
    {
        var found = DanceNameScanner.Scan("Mazurka de la valse", Index);

        Assert.Equal(2, found.Count);
    }
}
