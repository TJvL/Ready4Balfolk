using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Finding declared dance names inside a piece of text somebody else wrote.
/// </summary>
/// <remarks>
/// How the scanner reads across languages is covered by <see cref="DanceWordsTests"/>, which is
/// where the glue and the number words live. What is left here is the scanning itself: which name
/// wins when one sits inside another, that a word is only spent once, and that text carrying no
/// dance at all comes back with nothing rather than with a guess.
/// </remarks>
public sealed class DanceNameScannerTests
{
    private static readonly DanceListIndex Index = DanceListIndex.Build(new DanceList
    {
        Tags = ["france"],
        Dances =
        [
            TestData.CreateDance("andro", names: ["Andro"]),
            TestData.CreateDance("bourree", names: ["Bourrée"]),
            TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
            TestData.CreateDance("mazurka", names: ["Mazurka"])
        ]
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingToRead_FindsNothing(string? text) => Assert.Empty(DanceNameScanner.Scan(text, Index));

    [Fact]
    public void TextThatIsAllPunctuation_FindsNothing() =>
        // It folds away to nothing, and a scanner that reads an empty word list as "everything
        // matches" would put a dance on every file with a symbol in its name.
        Assert.Empty(DanceNameScanner.Scan("--- (!) ---", Index));

    [Fact]
    public void ANameInsideALongerWord_IsNotAMatch() =>
        // Whole words, not substrings: "Andro" lives inside "Androgyne" and inside nothing else
        // that matters, and matching it there would tag a track by its title.
        Assert.Empty(DanceNameScanner.Scan("Androgyne", Index));

    [Fact]
    public void ANameTheListDoesNotCarry_FindsNothing() =>
        Assert.Empty(DanceNameScanner.Scan("Naragonia - Sinner Man", Index));

    [Fact]
    public void TheLongerNameWins_AndSpendsTheWordsTheShorterOneNeeded()
    {
        // "Bourrée 3 temps" contains "Bourrée". Longest first claims all three words, so the plain
        // bourrée has nothing left to match: one file, one dance, and the specific one.
        var found = DanceNameScanner.Scan("Bourrée 3 temps", Index);

        Assert.Equal("bourree-3-temps", Assert.Single(found).Slug);
    }

    [Fact]
    public void TheShorterNameIsStillFound_WhenTheLongerOneIsNotThere() =>
        Assert.Equal("bourree", Assert.Single(DanceNameScanner.Scan("Bourrée du Berry", Index)).Slug);

    [Fact]
    public void OneDanceWrittenTwice_IsStillOneDance() =>
        // The answer is which dances are named, not how many times each was typed.
        Assert.Single(DanceNameScanner.Scan("Mazurka / Mazurka (reprise)", Index));

    [Fact]
    public void EveryMatch_SaysWhichNameMatched()
    {
        // The caller reports the name it found back to the user, so it has to be the name from the
        // list rather than whatever the file happened to call it.
        var found = DanceNameScanner.Scan("01. Bourrée 3 temps.mp3", Index);

        Assert.Equal(("bourree-3-temps", "bourree 3 temps"), Assert.Single(found));
    }

    [Fact]
    public void TwoDifferentDances_AreBothFound()
    {
        var found = DanceNameScanner.Scan("Andro / Mazurka", Index);

        Assert.Equal(["andro", "mazurka"], found.Select(match => match.Slug).Order());
    }
}
