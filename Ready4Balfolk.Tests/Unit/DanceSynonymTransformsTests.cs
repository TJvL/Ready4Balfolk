using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceSynonymTransformsTests
{
    // --- IsNameUnique ---

    [Fact]
    public void IsNameUnique_NewName_ReturnsTrue()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.True(DanceSynonymTransforms.IsNameUnique(list, "Polka"));
    }

    [Fact]
    public void IsNameUnique_MainName_ReturnsFalse()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.False(DanceSynonymTransforms.IsNameUnique(list, "Mazurka"));
    }

    [Fact]
    public void IsNameUnique_SynonymName_ReturnsFalse()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.False(DanceSynonymTransforms.IsNameUnique(list, "Mazurk"));
    }

    [Fact]
    public void IsNameUnique_CaseInsensitive()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.False(DanceSynonymTransforms.IsNameUnique(list, "mazurka"));
    }

    [Fact]
    public void IsNameUnique_ExcludesMain()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.True(DanceSynonymTransforms.IsNameUnique(list, "Mazurka", excludeMainIndex: 0));
    }

    [Fact]
    public void IsNameUnique_ExcludesSynonym()
    {
        var list = TestData.CreateSimpleSynonyms();
        Assert.True(DanceSynonymTransforms.IsNameUnique(list, "Mazurk",
            excludeMainIndex: 0, excludeSynonymIndex: 0));
    }

    // --- AddMainName ---

    [Fact]
    public void AddMainName_AppendsNewEntry()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.AddMainName(list);
        Assert.Equal(3, result.Count);
        Assert.Equal("New Dance", result[2].Name);
    }

    // --- DeleteMainName ---

    [Fact]
    public void DeleteMainName_RemovesEntry()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.DeleteMainName(list, 0);
        Assert.Single(result);
        Assert.Equal("Scottisch", result[0].Name);
    }

    // --- RenameMainName ---

    [Fact]
    public void RenameMainName_ChangesName()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.RenameMainName(list, 0, "Polka");
        Assert.Equal("Polka", result[0].Name);
    }

    // --- AddSynonym ---

    [Fact]
    public void AddSynonym_AppendsDefaultSynonym()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.AddSynonym(list, 0);
        Assert.Equal(3, result[0].Synonyms.Count());
        Assert.Equal("New Synonym", result[0].Synonyms.Last().Name);
    }

    // --- AddSynonymWithName ---

    [Fact]
    public void AddSynonymWithName_AppendsNamedSynonym()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.AddSynonymWithName(list, 0, "MazPolka");
        Assert.Equal(3, result[0].Synonyms.Count());
        Assert.Equal("MazPolka", result[0].Synonyms.Last().Name);
    }

    // --- DeleteSynonym ---

    [Fact]
    public void DeleteSynonym_RemovesSynonym()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.DeleteSynonym(list, 0, 0);
        Assert.Single(result[0].Synonyms);
        Assert.Equal("Mazou", result[0].Synonyms.First().Name);
    }

    // --- RenameSynonym ---

    [Fact]
    public void RenameSynonym_ChangesName()
    {
        var list = TestData.CreateSimpleSynonyms();
        var result = DanceSynonymTransforms.RenameSynonym(list, 0, 0, "MazRenamed");
        Assert.Equal("MazRenamed", result[0].Synonyms.First().Name);
    }
}
