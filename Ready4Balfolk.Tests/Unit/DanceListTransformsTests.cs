using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceListTransformsTests
{
    private readonly DanceList _list = TestData.CreateSimpleDanceList();

    [Fact]
    public void AddCategory_AtTheRoot_Appends()
    {
        var result = DanceListTransforms.AddCategory(_list, [], "Sweden");

        Assert.Equal(3, result.Categories.Count);
        Assert.Equal("Sweden", result.Categories[2].Name);
        Assert.Equal(1, result.Categories[2].Weight);
    }

    [Fact]
    public void AddCategory_InsideAnother_Nests()
    {
        var result = DanceListTransforms.AddCategory(_list, [0], "Waltzes");

        Assert.Equal("Waltzes", Assert.Single(result.Categories[0].Categories).Name);
    }

    [Fact]
    public void AddCategory_WithoutAName_GetsAFreeOne()
    {
        var once = DanceListTransforms.AddCategory(_list, []);
        var twice = DanceListTransforms.AddCategory(once, []);

        Assert.NotEqual(twice.Categories[2].Name, twice.Categories[3].Name);
    }

    [Fact]
    public void RenameCategory_LeavesEverythingElseAlone()
    {
        var result = DanceListTransforms.RenameCategory(_list, [1, 0], "Plinn suite");

        Assert.Equal("Plinn suite", result.Categories[1].Categories[0].Name);
        Assert.Equal("Common", result.Categories[0].Name);
        Assert.Equal(3, result.AllDances.Count());
    }

    [Fact]
    public void DeleteCategory_TakesItsDancesWithIt()
    {
        var result = DanceListTransforms.DeleteCategory(_list, [0]);

        Assert.Single(result.Categories);
        Assert.Equal(["plinn"], result.AllDances.Select(d => d.Slug));
    }

    [Fact]
    public void DeleteCategory_Nested_LeavesTheParent()
    {
        var result = DanceListTransforms.DeleteCategory(_list, [1, 0]);

        Assert.Equal("Bretagne", result.Categories[1].Name);
        Assert.Empty(result.Categories[1].Categories);
    }

    [Fact]
    public void AddDance_GetsASlugFromItsName()
    {
        var result = DanceListTransforms.AddDance(_list, [0], "Bourrée 3 temps");

        var added = result.Categories[0].Dances[^1];
        Assert.Equal("bourree-3-temps", added.Slug);
        Assert.Equal(["Bourrée 3 temps"], added.Names);
        Assert.Equal(1, added.Weight);
    }

    [Fact]
    public void AddDance_WhenTheSlugIsTaken_GetsADistinctOne()
    {
        var once = DanceListTransforms.AddDance(_list, [0], "Andro");
        var twice = DanceListTransforms.AddDance(once, [0], "Andro");

        Assert.Equal(["andro", "andro-2"], twice.Categories[0].Dances.Skip(2).Select(d => d.Slug));
    }

    [Fact]
    public void AddDance_AtTheRoot_IsRefused()
    {
        // Every dance sits in a category, so that randomisation always has a weight to apply.
        var result = DanceListTransforms.AddDance(_list, [], "Andro");

        Assert.Equal(3, result.AllDances.Count());
    }

    [Fact]
    public void DeleteDance_FindsItWhereverItIs()
    {
        var result = DanceListTransforms.DeleteDance(_list, "plinn");

        Assert.Equal(["mazurka", "scottish"], result.AllDances.Select(d => d.Slug));
    }

    [Fact]
    public void MoveDance_KeepsItsSlugNamesAndWeight()
    {
        var result = DanceListTransforms.MoveDance(_list, "plinn", [0]);

        var moved = Assert.Single(result.Categories[0].Dances, d => d.Slug == "plinn");
        Assert.Equal(2, moved.Weight);
        Assert.Equal(["Plinn"], moved.Names);
        Assert.Empty(result.Categories[1].Categories[0].Dances);
    }

    [Fact]
    public void AddName_AppendsWithoutChangingWhatIsDisplayed()
    {
        var result = DanceListTransforms.AddName(_list, "mazurka", "Mazurca");

        var dance = result.AllDances.First(d => d.Slug == "mazurka");
        Assert.Equal(["Mazurka", "Mazurk", "Mazurca"], dance.Names);
        Assert.Equal("Mazurka", dance.DisplayName);
    }

    [Fact]
    public void MoveName_ToTheFront_ChangesTheDisplayedSpellingAndNothingElse()
    {
        var result = DanceListTransforms.MoveName(_list, "mazurka", 1, 0);

        var dance = result.AllDances.First(d => d.Slug == "mazurka");
        Assert.Equal("Mazurk", dance.DisplayName);
        Assert.Equal("mazurka", dance.Slug);
        Assert.Equal(["Mazurk", "Mazurka"], dance.Names);
    }

    [Fact]
    public void RemoveNameAt_TakesTheRightOne()
    {
        var result = DanceListTransforms.RemoveNameAt(_list, "mazurka", 1);

        Assert.Equal(["Mazurka"], result.AllDances.First(d => d.Slug == "mazurka").Names);
    }

    [Fact]
    public void RemoveNameAt_TheLastName_IsRefused()
    {
        // A dance with no names could never be read or matched again.
        var result = DanceListTransforms.RemoveNameAt(_list, "plinn", 0);

        Assert.Equal(["Plinn"], result.AllDances.First(d => d.Slug == "plinn").Names);
    }

    [Fact]
    public void ReweightDance_ChangesOnlyThatDance()
    {
        var result = DanceListTransforms.ReweightDance(_list, "plinn", 5);

        Assert.Equal(5, result.AllDances.First(d => d.Slug == "plinn").Weight);
        Assert.Equal(1, result.AllDances.First(d => d.Slug == "mazurka").Weight);
    }

    [Fact]
    public void FindNameOwner_ReportsTheDanceThatAlreadyHasIt() => Assert.Equal("mazurka", DanceListTransforms.FindNameOwner(_list, "Mazurk"));

    [Fact]
    public void FindNameOwner_IgnoresTheDanceBeingEdited() => Assert.Null(DanceListTransforms.FindNameOwner(_list, "Mazurk", exceptSlug: "mazurka"));

    [Fact]
    public void FindNameOwner_FoldsBeforeComparing() => Assert.Equal("scottish", DanceListTransforms.FindNameOwner(_list, "schottísche"));

    [Fact]
    public void IsCategoryNameFree_OnlyLooksAtSiblings()
    {
        // "Suite plinn" is nested under Bretagne, so a root category may still take that name.
        Assert.True(DanceListTransforms.IsCategoryNameFree(_list, [], "Suite plinn"));
        Assert.False(DanceListTransforms.IsCategoryNameFree(_list, [], "Common"));
    }

    [Fact]
    public void IsCategoryNameFree_IgnoresTheCategoryBeingRenamed() => Assert.True(DanceListTransforms.IsCategoryNameFree(_list, [], "Common", excludeIndex: 0));

    [Fact]
    public void GenerateUniqueSlug_FoldsAccentsAndPunctuation()
    {
        Assert.Equal("kost-ar-choad", DanceListTransforms.GenerateUniqueSlug(DanceList.Empty, "Kost ar c'hoad"));
        Assert.Equal("pile-menu", DanceListTransforms.GenerateUniqueSlug(DanceList.Empty, "Pilé-menu"));
    }

    [Fact]
    public void ResolveCategory_FollowsThePath()
    {
        Assert.Equal("Suite plinn", DanceListTransforms.ResolveCategory(_list, [1, 0])?.Name);
        Assert.Null(DanceListTransforms.ResolveCategory(_list, [9]));
    }
}
