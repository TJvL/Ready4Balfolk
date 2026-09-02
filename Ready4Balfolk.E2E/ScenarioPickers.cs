using System.Collections.Concurrent;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.E2E;

/// <summary>The file pickers, answering with whatever the scenario said the person picked.</summary>
/// <remarks>
/// A picker belongs to the desktop, and headless has none. What a person decides in one is which
/// file comes back, so that is all a scenario decides here.
/// </remarks>
internal sealed class ScenarioPickers : IFilePickerService
{
    private readonly ConcurrentQueue<string> _willPick = new();

    /// <summary>Says what the person picks the next time something asks.</summary>
    public void TheyWillPick(string path) => _willPick.Enqueue(path);

    public Task<string?> PickFileToOpenAsync(string title, FileKind kind) => Task.FromResult(Next());

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(Next());

    public Task<string?> PickWhereToSaveAsync(string title, string suggestedName, FileKind kind) =>
        Task.FromResult(Next());

    /// <summary>The next answer, or nothing, which is a person closing the picker.</summary>
    private string? Next() => _willPick.TryDequeue(out var path) ? path : null;
}
