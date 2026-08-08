using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceListIndexTests
{
    private readonly DanceListIndex _sut = DanceListIndex.Build(TestData.CreateSimpleDanceList());

    [Fact]
    public void ResolveSlug_FirstName_ReturnsSlug() => Assert.Equal("mazurka", _sut.ResolveSlug("Mazurka"));

    [Fact]
    public void ResolveSlug_OtherName_ReturnsSameSlug() => Assert.Equal("mazurka", _sut.ResolveSlug("Mazurk"));

    [Fact]
    public void ResolveSlug_NestedCategory_IsIndexed() => Assert.Equal("plinn", _sut.ResolveSlug("Plinn"));

    [Fact]
    public void ResolveSlug_Unknown_ReturnsNull() => Assert.Null(_sut.ResolveSlug("Bourrée"));

    [Fact]
    public void ResolveSlug_CaseAndAccentInsensitive() => Assert.Equal("scottish", _sut.ResolveSlug("SCHOTTÍSCHE"));

    [Fact]
    public void ResolveSlug_Blank_ReturnsNull() => Assert.Null(_sut.ResolveSlug("   "));

    [Fact]
    public void DisplayNameFor_UsesTheFirstName() => Assert.Equal("Mazurka", _sut.DisplayNameFor("mazurka"));

    [Fact]
    public void DisplayNameFor_UnknownSlug_ReturnsTheSlug() => Assert.Equal("nope", _sut.DisplayNameFor("nope"));

    [Fact]
    public void IsNameTaken_ByAnotherDance_IsTrue() => Assert.True(_sut.IsNameTaken("Mazurk", exceptSlug: "scottish"));

    [Fact]
    public void IsNameTaken_ByTheSameDance_IsFalse() => Assert.False(_sut.IsNameTaken("Mazurk", exceptSlug: "mazurka"));

    [Fact]
    public void IsNameTaken_Unknown_IsFalse() => Assert.False(_sut.IsNameTaken("Andro"));

    [Fact]
    public void FoldedNamesLongestFirst_PrefersTheLongerName()
    {
        var list = new DanceList
        {
            Categories =
            [
                TestData.CreateCategory("Auvergne", dances:
                [
                    TestData.CreateDance("bourree", names: ["Bourrée"]),
                    TestData.CreateDance("bourree-3-temps", names: ["Bourrée 3 temps"])
                ])
            ]
        };

        var index = DanceListIndex.Build(list);

        // Scanning a filename walks this order, so the specific name has to come before the general
        // one sitting inside it.
        Assert.Equal("bourree 3 temps", index.FoldedNamesLongestFirst[0]);
    }

    [Fact]
    public void Build_NameClaimedTwice_KeepsTheFirstClaim()
    {
        var list = new DanceList
        {
            Categories =
            [
                TestData.CreateCategory("Somewhere", dances:
                [
                    TestData.CreateDance("first", names: ["Shared"]),
                    TestData.CreateDance("second", names: ["Shared"])
                ])
            ]
        };

        var index = DanceListIndex.Build(list);

        Assert.Equal("first", index.ResolveSlug("Shared"));
    }
}
