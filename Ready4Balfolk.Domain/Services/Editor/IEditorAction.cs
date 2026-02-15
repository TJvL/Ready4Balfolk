namespace Ready4Balfolk.Domain.Services.Editor;

public interface IEditorAction
{
    Task<EditorActionResult> ExecuteAsync();
    Task UndoAsync();
    string Description { get; }
}
