using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceTreeTransformsTests
{
    // --- IsLeafNameUnique ---

    [Fact]
    public void IsLeafNameUnique_UniqueLeaf_ReturnsTrue()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.True(DanceTreeTransforms.IsLeafNameUnique(tree, "Polka"));
    }

    [Fact]
    public void IsLeafNameUnique_DuplicateLeaf_ReturnsFalse()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.False(DanceTreeTransforms.IsLeafNameUnique(tree, "Mazurka"));
    }

    [Fact]
    public void IsLeafNameUnique_WithExclusion_ReturnsTrue()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.True(DanceTreeTransforms.IsLeafNameUnique(tree, "Mazurka",
            excludeParentPath: [0], excludeLeafIndex: 0));
    }

    [Fact]
    public void IsLeafNameUnique_CaseInsensitive()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.False(DanceTreeTransforms.IsLeafNameUnique(tree, "mazurka"));
    }

    // --- IsBranchNameUnique ---

    [Fact]
    public void IsBranchNameUnique_Unique_ReturnsTrue()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.True(DanceTreeTransforms.IsBranchNameUnique(tree, "Classical"));
    }

    [Fact]
    public void IsBranchNameUnique_Duplicate_ReturnsFalse()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.False(DanceTreeTransforms.IsBranchNameUnique(tree, "Folk"));
    }

    [Fact]
    public void IsBranchNameUnique_WithExclusion_ReturnsTrue()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.True(DanceTreeTransforms.IsBranchNameUnique(tree, "Folk", excludePath: [0]));
    }

    [Fact]
    public void IsBranchNameUnique_CaseInsensitive()
    {
        var tree = TestData.CreateSimpleTree();
        Assert.False(DanceTreeTransforms.IsBranchNameUnique(tree, "folk"));
    }

    // --- AddBranch ---

    [Fact]
    public void AddBranch_ToRoot_AppendsNewBranch()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddBranch(tree, []);
        Assert.Equal(3, result.Count);
        Assert.Equal("New Category", result[2].Name);
    }

    [Fact]
    public void AddBranch_Nested_AppendsToParent()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddBranch(tree, [0]);
        Assert.Single(result[0].Branches);
        Assert.Equal("New Category", result[0].Branches.First().Name);
    }

    [Fact]
    public void AddBranch_AutoNamesWhenDuplicate()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddBranch(tree, []);
        // First "New Category" added
        result = DanceTreeTransforms.AddBranch(result, []);
        Assert.Equal(4, result.Count);
        Assert.Equal("New Category 2", result[3].Name);
    }

    // --- RenameBranch ---

    [Fact]
    public void RenameBranch_ChangesName()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.RenameBranch(tree, [0], "Traditional");
        Assert.Equal("Traditional", result[0].Name);
        Assert.Equal("Bal", result[1].Name); // unchanged
    }

    // --- ReweightBranch ---

    [Fact]
    public void ReweightBranch_ChangesWeight()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.ReweightBranch(tree, [0], 5);
        Assert.Equal(5, result[0].Weight);
    }

    // --- DeleteBranch ---

    [Fact]
    public void DeleteBranch_Root_RemovesBranch()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.DeleteBranch(tree, [0]);
        Assert.Single(result);
        Assert.Equal("Bal", result[0].Name);
    }

    [Fact]
    public void DeleteBranch_Nested_RemovesChild()
    {
        // Add a nested branch first
        var tree = DanceTreeTransforms.AddBranch(TestData.CreateSimpleTree(), [0]);
        Assert.Single(tree[0].Branches);
        var result = DanceTreeTransforms.DeleteBranch(tree, [0, 0]);
        Assert.Empty(result[0].Branches);
    }

    [Fact]
    public void DeleteBranch_EmptyPath_ReturnsUnchanged()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.DeleteBranch(tree, []);
        Assert.Equal(tree.Count, result.Count);
    }

    // --- AddLeaf ---

    [Fact]
    public void AddLeaf_ToBranch_AppendsLeaf()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddLeaf(tree, [0]);
        Assert.Equal(3, result[0].Leafs.Count());
        Assert.Equal("New Dance", result[0].Leafs.Last().Name);
    }

    [Fact]
    public void AddLeaf_ToRoot_ReturnsUnchanged()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddLeaf(tree, []);
        Assert.Equal(tree.Count, result.Count);
    }

    [Fact]
    public void AddLeaf_AutoNamesWhenDuplicate()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.AddLeaf(tree, [0]);
        result = DanceTreeTransforms.AddLeaf(result, [0]);
        Assert.Equal(4, result[0].Leafs.Count());
        Assert.Equal("New Dance 2", result[0].Leafs.Last().Name);
    }

    // --- RenameLeaf ---

    [Fact]
    public void RenameLeaf_ChangesName()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.RenameLeaf(tree, [0], 0, "Polka");
        Assert.Equal("Polka", result[0].Leafs.First().Name);
    }

    // --- ReweightLeaf ---

    [Fact]
    public void ReweightLeaf_ChangesWeight()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.ReweightLeaf(tree, [0], 0, 10);
        Assert.Equal(10, result[0].Leafs.First().Weight);
    }

    // --- DeleteLeaf ---

    [Fact]
    public void DeleteLeaf_RemovesLeaf()
    {
        var tree = TestData.CreateSimpleTree();
        var result = DanceTreeTransforms.DeleteLeaf(tree, [0], 0);
        Assert.Single(result[0].Leafs);
        Assert.Equal("Schottische", result[0].Leafs.First().Name);
    }
}
