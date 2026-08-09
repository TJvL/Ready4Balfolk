using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class UnrecognisedValueClassifierTests
{
    private readonly DanceListIndex _index = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("hanter-dro", names: ["Hanter dro"]),
            TestData.CreateDance("bourree-2-temps", names: ["Bourrée 2 temps"]),
            TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"]),
            TestData.CreateDance("bourree-auvergnate", names: ["Bourrée Auvergnate"]),
            TestData.CreateDance("mazurka", names: ["Mazurka"]),
            TestData.CreateDance("scottish", names: ["Scottish"])
        ]
    });

    [Fact]
    public void AMisspelling_IsOneDecision()
    {
        // "Hanterdro" means "Hanter dro" in all 34 files.
        var (kind, slugs) = UnrecognisedValueClassifier.Classify("Hanterdro", _index);

        Assert.Equal(UnrecognisedKind.Misspelled, kind);
        Assert.Equal(["hanter-dro"], slugs);
    }

    [Fact]
    public void AValueInsideSeveralNames_IsTooGeneral()
    {
        // "Bourrée" across 50 tracks is some 2 temps, some 3 temps and some Auvergnate. Mapping the
        // value would invent 50 confident answers, which is the failure this whole feature exists
        // to prevent.
        var (kind, slugs) = UnrecognisedValueClassifier.Classify("Bourrée", _index);

        Assert.Equal(UnrecognisedKind.TooGeneral, kind);
        Assert.Equal(3, slugs.Count);
    }

    [Fact]
    public void TooGeneral_OffersNoWholesaleMap()
    {
        var value = new UnrecognisedValue
        {
            Value = "Bourrée",
            Kind = UnrecognisedKind.TooGeneral,
            Paths = ["/a.mp3"]
        };

        // Not a disabled button: no button. The question itself is wrong.
        Assert.False(value.CanMapAsAWhole);
    }

    [Fact]
    public void AValueInsideExactlyOneName_IsStillOneDecision()
    {
        var (kind, slugs) = UnrecognisedValueClassifier.Classify("Auvergnate", _index);

        Assert.Equal(UnrecognisedKind.Misspelled, kind);
        Assert.Equal(["bourree-auvergnate"], slugs);
    }

    [Fact]
    public void ABandName_IsUnknown()
    {
        var (kind, slugs) = UnrecognisedValueClassifier.Classify("Ar Re Yaouank", _index);

        Assert.Equal(UnrecognisedKind.Unknown, kind);
        Assert.Empty(slugs);
    }

    [Fact]
    public void AWordThatIsNotADance_IsUnknown()
    {
        // Somebody writes "(Folk)" after a title and means nothing by it. One ignore settles it.
        var (kind, _) = UnrecognisedValueClassifier.Classify("Folk", _index);

        Assert.Equal(UnrecognisedKind.Unknown, kind);
    }

    [Fact]
    public void SomethingNearTwoDances_IsNotOfferedAsAMap()
    {
        // Equally close to two names is a guess between them, not a misspelling of either.
        var (kind, _) = UnrecognisedValueClassifier.Classify("Bourrée 4 temps", _index);

        Assert.NotEqual(UnrecognisedKind.Misspelled, kind);
    }

    [Fact]
    public void ShortValues_GetALowerDistanceAllowance()
    {
        // One edit is a lot in a five-letter word, so "Polka" must not be called a misspelling of
        // "Mazurka" simply because both are short.
        var (kind, _) = UnrecognisedValueClassifier.Classify("Polka", _index);

        Assert.Equal(UnrecognisedKind.Unknown, kind);
    }

    [Fact]
    public void AccentAndCaseDifferencesAreNotMisspellingsAtAll()
    {
        // These fold to the same string, so the scanner already recognised them and they never
        // reach this classifier. Asserted so the folding rule is not quietly changed.
        Assert.Equal("scottish", _index.ResolveSlug("SCOTTISH"));
        Assert.Equal("bourree-2-temps", _index.ResolveSlug("bourree 2 temps"));
    }

    [Fact]
    public void ADanceTheListAlreadyKnows_IsNotAMisspellingOfItself()
    {
        // These tracks named this dance and another one too. Calling "Mazurka" a misspelling of
        // "Mazurka" and offering to map it is what this screen used to say.
        var (kind, slugs) = UnrecognisedValueClassifier.Classify("Mazurka", _index);

        Assert.Equal(UnrecognisedKind.Ambiguous, kind);
        Assert.Equal(["mazurka"], slugs);
    }

    [Fact]
    public void AnAmbiguousValue_IsDecidedPerTrack()
    {
        var value = new UnrecognisedValue
        {
            Value = "Mazurka",
            Kind = UnrecognisedKind.Ambiguous,
            Paths = ["/a.mp3", "/b.mp3"]
        };

        // One of them may be a mazurka-valse played as a valse. That is a decision about the track.
        Assert.False(value.CanMapAsAWhole);
    }

    [Fact]
    public void EmptyValue_IsUnknown()
    {
        var (kind, _) = UnrecognisedValueClassifier.Classify("   ", _index);

        Assert.Equal(UnrecognisedKind.Unknown, kind);
    }
}
