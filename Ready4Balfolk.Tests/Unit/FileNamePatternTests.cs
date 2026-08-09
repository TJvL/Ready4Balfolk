using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// A pattern is a user taking responsibility for a rule over their whole library, so it has to mean
/// exactly what it looks like it means, and be refused outright when it cannot.
/// </summary>
public sealed class FileNamePatternTests
{
    [Fact]
    public void ThreeFields_ArePickedOutInOrder()
    {
        var match = Match("%d - %a - %t", "Scottish - Bal O'Gadjo - Le badaud");

        Assert.Equal("Scottish", match?.Dance);
        Assert.Equal("Bal O'Gadjo", match?.Artist);
        Assert.Equal("Le badaud", match?.Title);
    }

    [Fact]
    public void TheLastField_TakesTheRestOfTheName()
    {
        // Otherwise "Le badaud - Live" quietly becomes "Le badaud" and the rest is lost.
        var match = Match("%a - %t", "Bal O'Gadjo - Le badaud - Live");

        Assert.Equal("Bal O'Gadjo", match?.Artist);
        Assert.Equal("Le badaud - Live", match?.Title);
    }

    [Fact]
    public void EarlierFields_StopAtTheNextLiteral()
    {
        var match = Match("%a - %t", "Naragonia - Mazurka");

        Assert.Equal("Naragonia", match?.Artist);
        Assert.Equal("Mazurka", match?.Title);
    }

    [Fact]
    public void AMatchIsWholeOrNothing() => Assert.Null(Match("%a - %t", "Something"));

    [Fact]
    public void ATrackNumber_MustBeANumber()
    {
        // A pattern claiming a number is there should fail a file that has none, rather than
        // swallowing a band name as a track number.
        Assert.NotNull(Match("%n - %t", "07 - Tregor"));
        Assert.Null(Match("%n - %t", "Ar Re Yaouank - Tregor"));
    }

    [Fact]
    public void ATrackNumber_IsNotAFieldOfTheTrack()
    {
        // It is read so a pattern can say where it sits, not because anything wants it.
        var match = Match("%n. %t", "05. Some Tune");

        Assert.Equal("05", match?.TrackNumber);
        Assert.Equal("Some Tune", match?.Title);
    }

    [Fact]
    public void Ignore_MatchesAndKeepsNothing()
    {
        var match = Match("%i - %a - %t", "2011 - Naragonia - Mazurka");

        Assert.Equal("Naragonia", match?.Artist);
        Assert.Equal("Mazurka", match?.Title);
    }

    [Fact]
    public void APatternAsksForTheExtension_OrIsNotShownOne()
    {
        var (withExtension, _) = FileNamePattern.Parse("%a - %t.%ex");
        var (without, _) = FileNamePattern.Parse("%a - %t");

        Assert.True(withExtension?.UsesExtension);
        Assert.False(without?.UsesExtension);
        Assert.NotNull(withExtension?.Match("Naragonia - Mazurka.mp3"));
    }

    [Theory]
    [InlineData("", PatternProblem.Empty)]
    [InlineData("   ", PatternProblem.Empty)]
    [InlineData("%z - %t", PatternProblem.UnknownToken)]
    [InlineData("just some text", PatternProblem.NoFields)]
    [InlineData("%n - %ex", PatternProblem.NoFields)]
    [InlineData("%a%t", PatternProblem.AdjacentFields)]
    [InlineData("%a - %a", PatternProblem.DuplicateField)]
    public void ABadPattern_IsRefusedAndSaysWhy(string text, PatternProblem expected)
    {
        var (pattern, problem) = FileNamePattern.Parse(text);

        Assert.Null(pattern);
        Assert.Equal(expected, problem);
    }

    [Fact]
    public void TwoIgnores_AreNotADuplicate() => Assert.NotNull(FileNamePattern.Parse("%i - %i - %t").Pattern);

    [Fact]
    public void LiteralsAreLiteral()
    {
        // A dot and a bracket are ordinary characters in a file name, not regular expression syntax.
        var match = Match("%a (%t)", "Naragonia (Mazurka)");

        Assert.Equal("Naragonia", match?.Artist);
        Assert.Equal("Mazurka", match?.Title);
    }

    private static FileNamePatternMatch? Match(string pattern, string fileName) =>
        FileNamePattern.Parse(pattern).Pattern?.Match(fileName);
}
