using System.Globalization;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Stores.Dances;

namespace Ready4Balfolk.Domain.Services.Editor;

/// <summary>An undoable edit to the dance list.</summary>
public sealed class DanceListAction : IEditorAction
{
    private readonly IDanceListStore _store;
    private readonly Func<DanceList, DanceList> _transform;
    private readonly Func<DanceList, EditorActionResult>? _validate;
    private DanceList _before = DanceList.Empty;
    private bool _executed;

    private DanceListAction(
        IDanceListStore store,
        string description,
        Func<DanceList, DanceList> transform,
        Func<DanceList, EditorActionResult>? validate = null)
    {
        _store = store;
        Description = description;
        _transform = transform;
        _validate = validate;
    }

    public string Description { get; }

    public async Task<EditorActionResult> ExecuteAsync()
    {
        var current = _store.Current;

        if (_validate is not null)
        {
            var validation = _validate(current);
            if (!validation.Success)
            {
                return validation;
            }
        }

        _before = current;
        await _store.UpdateAsync(_transform);
        _executed = true;
        return EditorActionResult.Ok();
    }

    /// <summary>
    /// Restores the list as it was. A no-op when the action was refused and never ran: undoing then
    /// would restore the empty snapshot it never took, which would wipe the list.
    /// </summary>
    public Task UndoAsync() => _executed ? _store.UpdateAsync(_ => _before) : Task.CompletedTask;

    public static DanceListAction AddCategory(IDanceListStore store, int[] parentPath)
        => new(store, DomainStrings.DanceListAction_AddCategory,
            list => DanceListTransforms.AddCategory(list, parentPath));

    public static DanceListAction RenameCategory(IDanceListStore store, int[] path, string newName)
        => new(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_RenameCategory, newName),
            list => DanceListTransforms.RenameCategory(list, path, newName),
            list => string.IsNullOrWhiteSpace(newName)
                ? EditorActionResult.Error(DomainStrings.DanceListAction_CategoryNameEmpty)
                : DanceListTransforms.IsCategoryNameFree(list, path[..^1], newName, path[^1])
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error(string.Format(
                        CultureInfo.CurrentCulture, DomainStrings.DanceListAction_CategoryNameExists, newName)));

    public static DanceListAction ReweightCategory(IDanceListStore store, int[] path, int newWeight)
        => new(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_ReweightCategory, newWeight),
            list => DanceListTransforms.ReweightCategory(list, path, newWeight),
            _ => newWeight >= 0
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceListAction_WeightMustNotBeNegative));

    public static DanceListAction DeleteCategory(IDanceListStore store, int[] path)
    {
        var name = DanceListTransforms.ResolveCategory(store.Current, path)?.Name ?? "?";
        return new DanceListAction(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_DeleteCategory, name),
            list => DanceListTransforms.DeleteCategory(list, path));
    }

    public static DanceListAction AddDance(IDanceListStore store, int[] categoryPath, string name)
        => new(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_AddDance, name),
            list => DanceListTransforms.AddDance(list, categoryPath, name),
            list => categoryPath.Length == 0
                ? EditorActionResult.Error(DomainStrings.DanceListAction_DanceNeedsACategory)
                : NameIsFree(list, name, exceptSlug: null));

    public static DanceListAction DeleteDance(IDanceListStore store, string slug)
    {
        var name = store.Index.DisplayNameFor(slug);
        return new DanceListAction(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_DeleteDance, name),
            list => DanceListTransforms.DeleteDance(list, slug));
    }

    public static DanceListAction MoveDance(IDanceListStore store, string slug, int[] targetCategoryPath)
    {
        var name = store.Index.DisplayNameFor(slug);
        return new DanceListAction(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_MoveDance, name),
            list => DanceListTransforms.MoveDance(list, slug, targetCategoryPath));
    }

    public static DanceListAction ReweightDance(IDanceListStore store, string slug, int newWeight)
        => new(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_ReweightDance, newWeight),
            list => DanceListTransforms.ReweightDance(list, slug, newWeight),
            _ => newWeight >= 0
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceListAction_WeightMustNotBeNegative));

    /// <summary>
    /// Adds a spelling. Refused when another dance already answers to it, which is the invariant
    /// that lets discovery answer with one dance rather than a set.
    /// </summary>
    public static DanceListAction AddName(IDanceListStore store, string slug, string name)
        => new(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_AddName, name),
            list => DanceListTransforms.AddName(list, slug, name),
            list => NameIsFree(list, name, exceptSlug: slug));

    public static DanceListAction RemoveName(IDanceListStore store, string slug, int index)
    {
        var name = store.Index.FindBySlug(slug) is { } dance && index >= 0 && index < dance.Names.Count
            ? dance.Names[index]
            : "?";

        return new DanceListAction(store,
            string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceListAction_RemoveName, name),
            list => DanceListTransforms.RemoveNameAt(list, slug, index),
            list => (list.AllDances.FirstOrDefault(d => string.Equals(d.Slug, slug, StringComparison.Ordinal))
                        ?.Names.Count ?? 0) > 1
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceListAction_LastNameCannotGo));
    }

    public static DanceListAction MoveName(IDanceListStore store, string slug, int fromIndex, int toIndex)
        => new(store, DomainStrings.DanceListAction_MoveName,
            list => DanceListTransforms.MoveName(list, slug, fromIndex, toIndex));

    private static EditorActionResult NameIsFree(DanceList list, string name, string? exceptSlug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return EditorActionResult.Error(DomainStrings.DanceListAction_NameEmpty);
        }

        var owner = DanceListTransforms.FindNameOwner(list, name, exceptSlug);
        return owner is null
            ? EditorActionResult.Ok()
            : EditorActionResult.Error(string.Format(
                CultureInfo.CurrentCulture,
                DomainStrings.DanceListAction_NameBelongsToAnotherDance,
                name,
                DanceListIndex.Build(list).DisplayNameFor(owner)));
    }
}
