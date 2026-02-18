using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class StringNormalizerTests
{
    [Fact]
    public void Normalize_Null_ReturnsEmpty() =>
        Assert.Equal("", StringNormalizer.Normalize(null!));

    [Fact]
    public void Normalize_Empty_ReturnsEmpty() =>
        Assert.Equal("", StringNormalizer.Normalize(""));

    [Fact]
    public void Normalize_Spaces_ReturnsEmpty() =>
        Assert.Equal("", StringNormalizer.Normalize("   "));

    [Fact]
    public void Normalize_TabNewline_ReturnsEmpty() =>
        Assert.Equal("", StringNormalizer.Normalize("\t\n"));

    [Fact]
    public void Normalize_RemovesAccents() => Assert.Equal("cafe", StringNormalizer.Normalize("café"));

    [Fact]
    public void Normalize_ConvertsToLowercase() => Assert.Equal("mazurka", StringNormalizer.Normalize("MAZURKA"));

    [Fact]
    public void Normalize_CollapsesWhitespace() => Assert.Equal("a b", StringNormalizer.Normalize("  a   b  "));

    [Fact]
    public void Normalize_TrimsInput() => Assert.Equal("hello", StringNormalizer.Normalize("  hello  "));

    [Fact]
    public void Normalize_RemovesNonAlphanumeric() =>
        Assert.Equal("hello world", StringNormalizer.Normalize("hello, world!"));

    [Fact]
    public void Normalize_PreservesDigits() => Assert.Equal("track 42", StringNormalizer.Normalize("Track 42"));

    [Fact]
    public void Normalize_ComplexAccentedString() => Assert.Equal("bourree", StringNormalizer.Normalize("Bourrée"));

    [Fact]
    public void Normalize_MixedCase_Accents_Punctuation() =>
        Assert.Equal("scotisch reinhardts", StringNormalizer.Normalize("Scotisch (Reinhardt's)"));
}
