using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Stores.Synonym;

namespace Ready4Balfolk.Domain.Services.Editor;

public sealed class DanceSynonymAction : IEditorAction
{
    private readonly IDanceSynonymStore _store;
    private readonly Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>> _transform;
    private readonly Func<IReadOnlyList<DanceMainName>, EditorActionResult>? _validate;
    private IReadOnlyList<DanceMainName> _before = [];

    public string Description { get; }

    private DanceSynonymAction(
        IDanceSynonymStore store,
        string description,
        Func<IReadOnlyList<DanceMainName>, IReadOnlyList<DanceMainName>> transform,
        Func<IReadOnlyList<DanceMainName>, EditorActionResult>? validate = null)
    {
        _store = store;
        Description = description;
        _transform = transform;
        _validate = validate;
    }

    public async Task<EditorActionResult> ExecuteAsync()
    {
        var current = _store.Current;

        if (_validate is not null)
        {
            var validation = _validate(current);
            if (!validation.Success)
                return validation;
        }

        _before = current;
        await _store.UpdateAsync(_transform);
        return EditorActionResult.Ok();
    }

    public Task UndoAsync()
        => _store.UpdateAsync(_ => _before);

    // Factory methods

    public static DanceSynonymAction AddMainName(IDanceSynonymStore store)
        => new(store, "Add dance name",
            DanceSynonymTransforms.AddMainName);

    public static DanceSynonymAction DeleteMainName(IDanceSynonymStore store, int index)
    {
        var name = index < store.Current.Count ? store.Current[index].Name : "?";
        return new(store, $"Delete dance name '{name}'",
            list => DanceSynonymTransforms.DeleteMainName(list, index));
    }

    public static DanceSynonymAction RenameMainName(IDanceSynonymStore store, int index, string newName)
        => new(store, $"Rename dance name to '{newName}'",
            list => DanceSynonymTransforms.RenameMainName(list, index, newName),
            list => !string.IsNullOrWhiteSpace(newName)
                ? DanceSynonymTransforms.IsNameUnique(list, newName, excludeMainIndex: index)
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error($"The name '{newName}' is already in use.")
                : EditorActionResult.Error("Dance name cannot be empty."));

    public static DanceSynonymAction AddSynonym(IDanceSynonymStore store, int mainNameIndex)
        => new(store, "Add synonym",
            list => DanceSynonymTransforms.AddSynonym(list, mainNameIndex));

    public static DanceSynonymAction AddSynonymWithName(
        IDanceSynonymStore store, int mainNameIndex, string name)
        => new(store, $"Add synonym '{name}'",
            list => DanceSynonymTransforms.AddSynonymWithName(list, mainNameIndex, name),
            list => !string.IsNullOrWhiteSpace(name)
                ? DanceSynonymTransforms.IsNameUnique(list, name)
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error($"The name '{name}' is already in use.")
                : EditorActionResult.Error("Synonym name cannot be empty."));

    public static DanceSynonymAction DeleteSynonym(IDanceSynonymStore store, int mainNameIndex, int synonymIndex)
    {
        var current = store.Current;
        var name = mainNameIndex < current.Count
            ? current[mainNameIndex].Synonyms.ToList() is { } syns && synonymIndex < syns.Count
                ? syns[synonymIndex].Name
                : "?"
            : "?";
        return new(store, $"Delete synonym '{name}'",
            list => DanceSynonymTransforms.DeleteSynonym(list, mainNameIndex, synonymIndex));
    }

    public static DanceSynonymAction RenameSynonym(
        IDanceSynonymStore store, int mainNameIndex, int synonymIndex, string newName)
        => new(store, $"Rename synonym to '{newName}'",
            list => DanceSynonymTransforms.RenameSynonym(list, mainNameIndex, synonymIndex, newName),
            list => !string.IsNullOrWhiteSpace(newName)
                ? DanceSynonymTransforms.IsNameUnique(list, newName, excludeMainIndex: mainNameIndex, excludeSynonymIndex: synonymIndex)
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error($"The name '{newName}' is already in use.")
                : EditorActionResult.Error("Synonym name cannot be empty."));
}
