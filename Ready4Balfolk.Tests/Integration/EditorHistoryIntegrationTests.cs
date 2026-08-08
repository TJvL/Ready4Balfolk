using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class EditorHistoryIntegrationTests : IDisposable
{
    private readonly IDanceListStore _store;
    private readonly BehaviorSubject<DanceList> _state;
    private readonly EditorHistoryService _history;

    public EditorHistoryIntegrationTests()
    {
        _state = new BehaviorSubject<DanceList>(TestData.CreateSimpleDanceList());
        _store = Substitute.For<IDanceListStore>();
        _store.Current.Returns(_ => _state.Value);
        _store.Index.Returns(_ => DanceListIndex.Build(_state.Value));
        _store.UpdateAsync(Arg.Any<Func<DanceList, DanceList>>())
            .Returns(ci =>
            {
                var transform = ci.Arg<Func<DanceList, DanceList>>()!;
                _state.OnNext(transform(_state.Value));
                return Task.CompletedTask;
            });

        _history = new EditorHistoryService(new ConsoleLoggerService());
    }

    [Fact]
    public async Task DoThenUndo_RestoresState()
    {
        var action = DanceListAction.AddCategory(_store, []);
        await _history.DoActionAsync(action);
        Assert.Equal(3, _state.Value.Categories.Count);

        await _history.UndoAsync();
        Assert.Equal(2, _state.Value.Categories.Count);
    }

    [Fact]
    public async Task DoThenUndoThenRedo_ReappliesAction()
    {
        var action = DanceListAction.AddCategory(_store, []);
        await _history.DoActionAsync(action);
        await _history.UndoAsync();
        await _history.RedoAsync();

        Assert.Equal(3, _state.Value.Categories.Count);
    }

    [Fact]
    public async Task MultipleActions_UndoAll()
    {
        await _history.DoActionAsync(DanceListAction.AddCategory(_store, []));
        Assert.Equal(3, _state.Value.Categories.Count);

        await _history.DoActionAsync(DanceListAction.AddDance(_store, [0], "Andro"));
        Assert.Equal(3, _state.Value.Categories[0].Dances.Count);

        // Undo the dance
        await _history.UndoAsync();
        Assert.Equal(2, _state.Value.Categories[0].Dances.Count);

        // Undo the category
        await _history.UndoAsync();
        Assert.Equal(2, _state.Value.Categories.Count);
    }

    [Fact]
    public async Task UndoSomeThenRedo()
    {
        await _history.DoActionAsync(DanceListAction.AddCategory(_store, []));
        await _history.DoActionAsync(DanceListAction.RenameCategory(_store, [0], "Trad"));

        await _history.UndoAsync();
        Assert.Equal("Common", _state.Value.Categories[0].Name);

        await _history.RedoAsync();
        Assert.Equal("Trad", _state.Value.Categories[0].Name);
    }

    [Fact]
    public async Task DoAfterUndo_ClearsRedoStack()
    {
        await _history.DoActionAsync(DanceListAction.AddCategory(_store, []));
        await _history.UndoAsync();

        var canRedo = false;
        using var sub = _history.CanRedo.Subscribe(v => canRedo = v);

        await _history.DoActionAsync(DanceListAction.AddDance(_store, [0], "Andro"));

        Assert.False(canRedo);
    }

    public void Dispose()
    {
        _history.Dispose();
        _state.Dispose();
    }
}
