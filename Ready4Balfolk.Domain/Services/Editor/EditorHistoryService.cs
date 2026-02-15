using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Services.Editor;

public sealed class EditorHistoryService(ILoggerService loggerService) : IEditorHistoryService, IDisposable
{
    public IObservable<bool> CanUndo => _canUndo.AsObservable();
    public IObservable<bool> CanRedo => _canRedo.AsObservable();
    public IObservable<string?> UndoDescription => _undoDescription.AsObservable();
    public IObservable<string?> RedoDescription => _redoDescription.AsObservable();

    private readonly BehaviorSubject<bool> _canUndo = new(false);
    private readonly BehaviorSubject<bool> _canRedo = new(false);
    private readonly BehaviorSubject<string?> _undoDescription = new(null);
    private readonly BehaviorSubject<string?> _redoDescription = new(null);
    private readonly Stack<IEditorAction> _undoStack = new();
    private readonly Stack<IEditorAction> _redoStack = new();

    public async Task<EditorActionResult> DoActionAsync(IEditorAction editorAction)
    {
        var result = await editorAction.ExecuteAsync();
        if (!result.Success)
            return result;

        _undoStack.Push(editorAction);
        _redoStack.Clear();
        NotifyChanges();

        await loggerService.DebugAsync($"EditorHistory: Did '{editorAction.Description}'");
        return result;
    }

    public async Task UndoAsync()
    {
        if (_undoStack.Count == 0)
            return;

        var editorAction = _undoStack.Pop();
        await editorAction.UndoAsync();
        _redoStack.Push(editorAction);
        NotifyChanges();

        await loggerService.DebugAsync($"EditorHistory: Undid '{editorAction.Description}'");
    }

    public async Task RedoAsync()
    {
        if (_redoStack.Count == 0)
            return;

        var editorAction = _redoStack.Pop();
        await editorAction.ExecuteAsync();
        _undoStack.Push(editorAction);
        NotifyChanges();

        await loggerService.DebugAsync($"EditorHistory: Redid '{editorAction.Description}'");
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyChanges();
    }

    private void NotifyChanges()
    {
        _canUndo.OnNext(_undoStack.Count > 0);
        _canRedo.OnNext(_redoStack.Count > 0);
        _undoDescription.OnNext(_undoStack.Count > 0 ? _undoStack.Peek().Description : null);
        _redoDescription.OnNext(_redoStack.Count > 0 ? _redoStack.Peek().Description : null);
    }

    public void Dispose()
    {
        _canUndo.Dispose();
        _canRedo.Dispose();
        _undoDescription.Dispose();
        _redoDescription.Dispose();
    }
}
