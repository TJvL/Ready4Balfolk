using NSubstitute;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Tests.Unit;

public sealed class EditorHistoryServiceTests : IDisposable
{
    private readonly ILoggerService _logger = Substitute.For<ILoggerService>();
    private readonly EditorHistoryService _sut;

    public EditorHistoryServiceTests()
    {
        _sut = new EditorHistoryService(_logger);
    }

    private static IEditorAction CreateSuccessAction(string description = "Test Action")
    {
        var action = Substitute.For<IEditorAction>();
        action.Description.Returns(description);
        action.ExecuteAsync().Returns(EditorActionResult.Ok());
        action.UndoAsync().Returns(Task.CompletedTask);
        return action;
    }

    private static IEditorAction CreateFailingAction()
    {
        var action = Substitute.For<IEditorAction>();
        action.Description.Returns("Failing Action");
        action.ExecuteAsync().Returns(EditorActionResult.Error("Failed"));
        return action;
    }

    [Fact]
    public async Task DoAction_Success_PushesUndoAndClearsRedo()
    {
        bool canUndo = false, canRedo = false;
        using var undoSub = _sut.CanUndo.Subscribe(v => canUndo = v);
        using var redoSub = _sut.CanRedo.Subscribe(v => canRedo = v);

        var result = await _sut.DoActionAsync(CreateSuccessAction());

        Assert.True(result.Success);
        Assert.True(canUndo);
        Assert.False(canRedo);
    }

    [Fact]
    public async Task DoAction_Failure_DoesNotPush()
    {
        var canUndo = false;
        using var sub = _sut.CanUndo.Subscribe(v => canUndo = v);

        var result = await _sut.DoActionAsync(CreateFailingAction());

        Assert.False(result.Success);
        Assert.False(canUndo);
    }

    [Fact]
    public async Task Undo_PopsToRedo()
    {
        bool canUndo = false, canRedo = false;
        using var undoSub = _sut.CanUndo.Subscribe(v => canUndo = v);
        using var redoSub = _sut.CanRedo.Subscribe(v => canRedo = v);

        var action = CreateSuccessAction();
        await _sut.DoActionAsync(action);
        await _sut.UndoAsync();

        Assert.False(canUndo);
        Assert.True(canRedo);
        await action.Received(1).UndoAsync();
    }

    [Fact]
    public async Task Redo_PopsToUndo()
    {
        bool canUndo = false, canRedo = false;
        using var undoSub = _sut.CanUndo.Subscribe(v => canUndo = v);
        using var redoSub = _sut.CanRedo.Subscribe(v => canRedo = v);

        var action = CreateSuccessAction();
        await _sut.DoActionAsync(action);
        await _sut.UndoAsync();
        await _sut.RedoAsync();

        Assert.True(canUndo);
        Assert.False(canRedo);
        // ExecuteAsync called once for Do and once for Redo
        await action.Received(2).ExecuteAsync();
    }

    [Fact]
    public async Task UndoDescription_ReflectsTopOfStack()
    {
        string? desc = null;
        using var sub = _sut.UndoDescription.Subscribe(v => desc = v);

        Assert.Null(desc);

        await _sut.DoActionAsync(CreateSuccessAction("Action A"));
        Assert.Equal("Action A", desc);

        await _sut.DoActionAsync(CreateSuccessAction("Action B"));
        Assert.Equal("Action B", desc);

        await _sut.UndoAsync();
        Assert.Equal("Action A", desc);
    }

    [Fact]
    public async Task RedoDescription_ReflectsTopOfStack()
    {
        string? desc = null;
        using var sub = _sut.RedoDescription.Subscribe(v => desc = v);

        await _sut.DoActionAsync(CreateSuccessAction("Action A"));
        await _sut.UndoAsync();

        Assert.Equal("Action A", desc);
    }

    [Fact]
    public async Task Clear_EmptiesBothStacks()
    {
        bool canUndo = false, canRedo = false;
        using var undoSub = _sut.CanUndo.Subscribe(v => canUndo = v);
        using var redoSub = _sut.CanRedo.Subscribe(v => canRedo = v);

        await _sut.DoActionAsync(CreateSuccessAction());
        await _sut.UndoAsync();

        _sut.Clear();

        Assert.False(canUndo);
        Assert.False(canRedo);
    }

    [Fact]
    public async Task Undo_EmptyStack_IsSafe() => await _sut.UndoAsync();

    [Fact]
    public async Task Redo_EmptyStack_IsSafe() => await _sut.RedoAsync();

    [Fact]
    public async Task DoAfterUndo_ClearsRedoStack()
    {
        var canRedo = false;
        using var sub = _sut.CanRedo.Subscribe(v => canRedo = v);

        await _sut.DoActionAsync(CreateSuccessAction("A"));
        await _sut.UndoAsync();
        Assert.True(canRedo);

        await _sut.DoActionAsync(CreateSuccessAction("B"));
        Assert.False(canRedo);
    }

    public void Dispose() => _sut.Dispose();
}
