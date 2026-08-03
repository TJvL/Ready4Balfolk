using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceSynonymActionTests : IDisposable
{
    private readonly IDanceSynonymStore _store;
    private readonly BehaviorSubject<IReadOnlyList<DanceMainName>> _state;

    public DanceSynonymActionTests()
    {
        _state = new BehaviorSubject<IReadOnlyList<DanceMainName>>(TestData.CreateSimpleSynonyms());
        _store = Substitute.For<IDanceSynonymStore>();
        _store.Current.Returns(_ => _state.Value);
        _store.UpdateAsync(Arg.Any<Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>>>())
            .Returns(ci =>
            {
                var transform = ci.Arg<Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>>>()!;
                _state.OnNext(transform(_state.Value));
                return Task.CompletedTask;
            });
    }

    // --- AddMainName ---

    [Fact]
    public async Task AddMainName_Execute_AddsEntry()
    {
        var action = DanceSynonymAction.AddMainName(_store);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(3, _state.Value.Count);
        Assert.Equal("New Dance", _state.Value[2].Name);
    }

    [Fact]
    public async Task AddMainName_Undo_RestoresOriginal()
    {
        var action = DanceSynonymAction.AddMainName(_store);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value.Count);
    }

    // --- DeleteMainName ---

    [Fact]
    public async Task DeleteMainName_Execute_RemovesEntry()
    {
        var action = DanceSynonymAction.DeleteMainName(_store, 0);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Single(_state.Value);
        Assert.Equal("Scottisch", _state.Value[0].Name);
    }

    [Fact]
    public async Task DeleteMainName_Undo_RestoresOriginal()
    {
        var action = DanceSynonymAction.DeleteMainName(_store, 0);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value.Count);
        Assert.Equal("Mazurka", _state.Value[0].Name);
    }

    // --- RenameMainName ---

    [Fact]
    public async Task RenameMainName_Execute_ChangesName()
    {
        var action = DanceSynonymAction.RenameMainName(_store, 0, "Polka");
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal("Polka", _state.Value[0].Name);
    }

    [Fact]
    public async Task RenameMainName_EmptyName_ReturnsError()
    {
        var action = DanceSynonymAction.RenameMainName(_store, 0, "");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameMainName_DuplicateName_ReturnsError()
    {
        var action = DanceSynonymAction.RenameMainName(_store, 0, "Scottisch");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameMainName_Undo_RestoresOriginal()
    {
        var action = DanceSynonymAction.RenameMainName(_store, 0, "Polka");
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal("Mazurka", _state.Value[0].Name);
    }

    // --- AddSynonym ---

    [Fact]
    public async Task AddSynonym_Execute_AddsSynonym()
    {
        var action = DanceSynonymAction.AddSynonym(_store, 0);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal(3, _state.Value[0].Synonyms.Count());
    }

    [Fact]
    public async Task AddSynonym_Undo_RestoresOriginal()
    {
        var action = DanceSynonymAction.AddSynonym(_store, 0);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value[0].Synonyms.Count());
    }

    // --- AddSynonymWithName ---

    [Fact]
    public async Task AddSynonymWithName_Execute_AddsNamedSynonym()
    {
        var action = DanceSynonymAction.AddSynonymWithName(_store, 0, "MazNew");
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal("MazNew", _state.Value[0].Synonyms.Last().Name);
    }

    [Fact]
    public async Task AddSynonymWithName_EmptyName_ReturnsError()
    {
        var action = DanceSynonymAction.AddSynonymWithName(_store, 0, "");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddSynonymWithName_DuplicateName_ReturnsError()
    {
        var action = DanceSynonymAction.AddSynonymWithName(_store, 0, "Mazurk");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    // --- DeleteSynonym ---

    [Fact]
    public async Task DeleteSynonym_Execute_RemovesSynonym()
    {
        var action = DanceSynonymAction.DeleteSynonym(_store, 0, 0);
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Single(_state.Value[0].Synonyms);
    }

    [Fact]
    public async Task DeleteSynonym_Undo_RestoresOriginal()
    {
        var action = DanceSynonymAction.DeleteSynonym(_store, 0, 0);
        await action.ExecuteAsync();
        await action.UndoAsync();

        Assert.Equal(2, _state.Value[0].Synonyms.Count());
        Assert.Equal("Mazurk", _state.Value[0].Synonyms.First().Name);
    }

    // --- RenameSynonym ---

    [Fact]
    public async Task RenameSynonym_Execute_ChangesName()
    {
        var action = DanceSynonymAction.RenameSynonym(_store, 0, 0, "MazRenamed");
        var result = await action.ExecuteAsync();

        Assert.True(result.Success);
        Assert.Equal("MazRenamed", _state.Value[0].Synonyms.First().Name);
    }

    [Fact]
    public async Task RenameSynonym_EmptyName_ReturnsError()
    {
        var action = DanceSynonymAction.RenameSynonym(_store, 0, 0, "");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RenameSynonym_DuplicateName_ReturnsError()
    {
        var action = DanceSynonymAction.RenameSynonym(_store, 0, 0, "Mazou");
        var result = await action.ExecuteAsync();

        Assert.False(result.Success);
    }

    public void Dispose() => _state.Dispose();
}
