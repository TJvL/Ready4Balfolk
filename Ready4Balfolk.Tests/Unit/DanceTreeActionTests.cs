using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceTreeActionTests : IDisposable
{
    private readonly IDanceTreeStore _store;
    private readonly BehaviorSubject<IReadOnlyList<DanceBranch>> _state;

    public DanceTreeActionTests()
    {
        _state = new BehaviorSubject<IReadOnlyList<DanceBranch>>(TestData.CreateSimpleTree());
        _store = Substitute.For<IDanceTreeStore>();
        _store.Current.Returns(_ => _state.Value);
        _store.UpdateAsync(Arg.Any<Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>>>())
            .Returns(ci =>
            {
                var transform = ci.Arg<Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>>>()!;
                _state.OnNext(transform(_state.Value));
                return Task.CompletedTask;
            });
    }

    // --- AddBranch ---

    [Fact]
    public async Task AddBranch_Execute_AddsBranch()
    {
        var action = DanceTreeAction.AddBranch(_store, []);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(3, _state.Value.Count);
    }

    [Fact]
    public async Task AddBranch_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.AddBranch(_store, []);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value.Count);
    }

    // --- AddLeaf ---

    [Fact]
    public async Task AddLeaf_Execute_AddsLeaf()
    {
        var action = DanceTreeAction.AddLeaf(_store, [0]);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(3, _state.Value[0].Leafs.Count());
    }

    [Fact]
    public async Task AddLeaf_RootPath_ReturnsError()
    {
        var action = DanceTreeAction.AddLeaf(_store, []);
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
        Assert.Contains(DomainStrings.DanceTreeAction_CannotAddDanceToRoot, result.ErrorMessage!);
    }

    [Fact]
    public async Task AddLeaf_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.AddLeaf(_store, [0]);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value[0].Leafs.Count());
    }

    // --- RenameBranch ---

    [Fact]
    public async Task RenameBranch_Execute_ChangesName()
    {
        var action = DanceTreeAction.RenameBranch(_store, [0], "Traditional");
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal("Traditional", _state.Value[0].Name);
    }

    [Fact]
    public async Task RenameBranch_EmptyName_ReturnsError()
    {
        var action = DanceTreeAction.RenameBranch(_store, [0], "  ");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameBranch_DuplicateName_ReturnsError()
    {
        var action = DanceTreeAction.RenameBranch(_store, [0], "Bal");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameBranch_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.RenameBranch(_store, [0], "Traditional");
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal("Folk", _state.Value[0].Name);
    }

    // --- ReweightBranch ---

    [Fact]
    public async Task ReweightBranch_Execute_ChangesWeight()
    {
        var action = DanceTreeAction.ReweightBranch(_store, [0], 5);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(5, _state.Value[0].Weight);
    }

    [Fact]
    public async Task ReweightBranch_NegativeWeight_ReturnsError()
    {
        var action = DanceTreeAction.ReweightBranch(_store, [0], -1);
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    // --- DeleteBranch ---

    [Fact]
    public async Task DeleteBranch_Execute_RemovesBranch()
    {
        var action = DanceTreeAction.DeleteBranch(_store, [0]);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Single(_state.Value);
    }

    [Fact]
    public async Task DeleteBranch_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.DeleteBranch(_store, [0]);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value.Count);
        Assert.Equal("Folk", _state.Value[0].Name);
    }

    // --- RenameLeaf ---

    [Fact]
    public async Task RenameLeaf_Execute_ChangesName()
    {
        var action = DanceTreeAction.RenameLeaf(_store, [0], 0, "Polka");
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal("Polka", _state.Value[0].Leafs.First().Name);
    }

    [Fact]
    public async Task RenameLeaf_EmptyName_ReturnsError()
    {
        var action = DanceTreeAction.RenameLeaf(_store, [0], 0, "");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameLeaf_DuplicateName_ReturnsError()
    {
        var action = DanceTreeAction.RenameLeaf(_store, [0], 0, "Schottische");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameLeaf_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.RenameLeaf(_store, [0], 0, "Polka");
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal("Mazurka", _state.Value[0].Leafs.First().Name);
    }

    // --- ReweightLeaf ---

    [Fact]
    public async Task ReweightLeaf_Execute_ChangesWeight()
    {
        var action = DanceTreeAction.ReweightLeaf(_store, [0], 0, 10);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(10, _state.Value[0].Leafs.First().Weight);
    }

    [Fact]
    public async Task ReweightLeaf_NegativeWeight_ReturnsError()
    {
        var action = DanceTreeAction.ReweightLeaf(_store, [0], 0, -1);
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    // --- DeleteLeaf ---

    [Fact]
    public async Task DeleteLeaf_Execute_RemovesLeaf()
    {
        var action = DanceTreeAction.DeleteLeaf(_store, [0], 0);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Single(_state.Value[0].Leafs);
    }

    [Fact]
    public async Task DeleteLeaf_Undo_RestoresOriginal()
    {
        var action = DanceTreeAction.DeleteLeaf(_store, [0], 0);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value[0].Leafs.Count());
        Assert.Equal("Mazurka", _state.Value[0].Leafs.First().Name);
    }

    public void Dispose() => _state.Dispose();
}
