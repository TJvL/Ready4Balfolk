namespace Ready4Balfolk.Domain.Services.Editor;

public interface IEditorHistoryService
{
    IObservable<bool> CanUndo { get; }
    IObservable<bool> CanRedo { get; }
    IObservable<string?> UndoDescription { get; }
    IObservable<string?> RedoDescription { get; }

    Task<EditorActionResult> DoActionAsync(IEditorAction editorAction);
    Task UndoAsync();
    Task RedoAsync();
    void Clear();
}
