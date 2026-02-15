using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class EditorHistoryIntegrationTests : IDisposable
{
    private readonly IDanceTreeStore _store;
    private readonly BehaviorSubject<IReadOnlyList<DanceBranch>> _state;
    private readonly EditorHistoryService _history;

    public EditorHistoryIntegrationTests()
    {
        _state = new BehaviorSubject<IReadOnlyList<DanceBranch>>(TestData.CreateSimpleTree());
        _store = Substitute.For<IDanceTreeStore>();
        _store.Current.Returns(_ => _state.Value);
        _store.UpdateAsync(Arg.Any<Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>>>())
            .Returns(ci =>
            {
                var transform = ci.Arg<Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>>>();
                _state.OnNext(transform(_state.Value));
                return Task.CompletedTask;
            });

        _history = new EditorHistoryService(new ConsoleLoggerService());
    }

    [Fact]
    public async Task DoThenUndo_RestoresState()
    {
        var action = DanceTreeAction.AddBranch(_store, []);
        await _history.DoActionAsync(action);
        Assert.Equal(3, _state.Value.Count);

        await _history.UndoAsync();
        Assert.Equal(2, _state.Value.Count);
    }

    [Fact]
    public async Task DoThenUndoThenRedo_ReappliesAction()
    {
        var action = DanceTreeAction.AddBranch(_store, []);
        await _history.DoActionAsync(action);
        await _history.UndoAsync();
        await _history.RedoAsync();

        Assert.Equal(3, _state.Value.Count);
    }

    [Fact]
    public async Task MultipleActions_UndoAll()
    {
        await _history.DoActionAsync(DanceTreeAction.AddBranch(_store, []));
        Assert.Equal(3, _state.Value.Count);

        await _history.DoActionAsync(DanceTreeAction.AddLeaf(_store, [0]));
        Assert.Equal(3, _state.Value[0].Leafs.Count());

        // Undo leaf add
        await _history.UndoAsync();
        Assert.Equal(2, _state.Value[0].Leafs.Count());

        // Undo branch add
        await _history.UndoAsync();
        Assert.Equal(2, _state.Value.Count);
    }

    [Fact]
    public async Task UndoSomeThenRedo()
    {
        await _history.DoActionAsync(DanceTreeAction.AddBranch(_store, []));
        await _history.DoActionAsync(DanceTreeAction.RenameBranch(_store, [0], "Trad"));

        await _history.UndoAsync();
        Assert.Equal("Folk", _state.Value[0].Name);

        await _history.RedoAsync();
        Assert.Equal("Trad", _state.Value[0].Name);
    }

    [Fact]
    public async Task DoAfterUndo_ClearsRedoStack()
    {
        await _history.DoActionAsync(DanceTreeAction.AddBranch(_store, []));
        await _history.UndoAsync();

        var canRedo = false;
        using var sub = _history.CanRedo.Subscribe(v => canRedo = v);

        await _history.DoActionAsync(DanceTreeAction.AddLeaf(_store, [0]));

        Assert.False(canRedo);
    }

    public void Dispose()
    {
        _history.Dispose();
        _state.Dispose();
    }
}
