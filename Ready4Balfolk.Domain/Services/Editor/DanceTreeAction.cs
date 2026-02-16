using System.Globalization;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Stores.Tree;

namespace Ready4Balfolk.Domain.Services.Editor;

public sealed class DanceTreeAction : IEditorAction
{
    private readonly IDanceTreeStore _store;
    private readonly Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> _transform;
    private readonly Func<IReadOnlyList<DanceBranch>, EditorActionResult>? _validate;
    private IReadOnlyList<DanceBranch> _before = [];

    public string Description { get; }

    private DanceTreeAction(
        IDanceTreeStore store,
        string description,
        Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>> transform,
        Func<IReadOnlyList<DanceBranch>, EditorActionResult>? validate = null)
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
            {
                return validation;
            }
        }

        _before = current;
        await _store.UpdateAsync(_transform);
        return EditorActionResult.Ok();
    }

    public Task UndoAsync()
        => _store.UpdateAsync(_ => _before);

    // Factory methods

    public static DanceTreeAction AddBranch(IDanceTreeStore store, int[] path)
        => new(store, DomainStrings.DanceTreeAction_AddCategory,
            roots => DanceTreeTransforms.AddBranch(roots, path));

    public static DanceTreeAction AddLeaf(IDanceTreeStore store, int[] path)
        => new(store, DomainStrings.DanceTreeAction_AddDance,
            roots => DanceTreeTransforms.AddLeaf(roots, path),
            _ => path.Length > 0
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceTreeAction_CannotAddDanceToRoot));

    public static DanceTreeAction AddLeafWithName(IDanceTreeStore store, int[] path, string name)
        => new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_AddDanceWithName, name),
            roots => DanceTreeTransforms.AddLeaf(roots, path, name),
            roots => path.Length == 0
                ? EditorActionResult.Error(DomainStrings.DanceTreeAction_CannotAddDanceToRoot)
                : string.IsNullOrWhiteSpace(name)
                    ? EditorActionResult.Error(DomainStrings.DanceTreeAction_DanceNameEmpty)
                    : DanceTreeTransforms.IsLeafNameUnique(roots, name)
                        ? EditorActionResult.Ok()
                        : EditorActionResult.Error(string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_DanceNameExists, name)));

    public static DanceTreeAction DeleteBranch(IDanceTreeStore store, int[] path)
    {
        var name = ResolveBranchName(store.Current, path);
        return new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_DeleteCategory, name),
            roots => DanceTreeTransforms.DeleteBranch(roots, path));
    }

    public static DanceTreeAction DeleteLeaf(IDanceTreeStore store, int[] parentPath, int leafIndex)
    {
        var name = ResolveLeafName(store.Current, parentPath, leafIndex);
        return new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_DeleteDance, name),
            roots => DanceTreeTransforms.DeleteLeaf(roots, parentPath, leafIndex));
    }

    public static DanceTreeAction RenameBranch(IDanceTreeStore store, int[] path, string newName)
        => new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_RenameCategory, newName),
            roots => DanceTreeTransforms.RenameBranch(roots, path, newName),
            roots => string.IsNullOrWhiteSpace(newName)
                ? EditorActionResult.Error(DomainStrings.DanceTreeAction_CategoryNameEmpty)
                : DanceTreeTransforms.IsBranchNameUnique(roots, newName, excludePath: path)
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error(string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_CategoryNameExists, newName)));

    public static DanceTreeAction ReweightBranch(IDanceTreeStore store, int[] path, int newWeight)
        => new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_ReweightCategory, newWeight),
            roots => DanceTreeTransforms.ReweightBranch(roots, path, newWeight),
            _ => newWeight >= 0
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceTreeAction_WeightMustBePositive));

    public static DanceTreeAction RenameLeaf(IDanceTreeStore store, int[] parentPath, int leafIndex, string newName)
        => new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_RenameDance, newName),
            roots => DanceTreeTransforms.RenameLeaf(roots, parentPath, leafIndex, newName),
            roots => string.IsNullOrWhiteSpace(newName)
                ? EditorActionResult.Error(DomainStrings.DanceTreeAction_DanceNameEmpty)
                : DanceTreeTransforms.IsLeafNameUnique(roots, newName, excludeParentPath: parentPath, excludeLeafIndex: leafIndex)
                    ? EditorActionResult.Ok()
                    : EditorActionResult.Error(string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_DanceNameExists, newName)));

    public static DanceTreeAction ReweightLeaf(IDanceTreeStore store, int[] parentPath, int leafIndex, int newWeight)
        => new(store, string.Format(CultureInfo.CurrentCulture, DomainStrings.DanceTreeAction_ReweightDance, newWeight),
            roots => DanceTreeTransforms.ReweightLeaf(roots, parentPath, leafIndex, newWeight),
            _ => newWeight >= 0
                ? EditorActionResult.Ok()
                : EditorActionResult.Error(DomainStrings.DanceTreeAction_WeightMustBePositive));

    private static string ResolveBranchName(IReadOnlyList<DanceBranch> roots, int[] path)
    {
        var level = roots;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] >= level.Count)
            {
                return "?";
            }

            if (i == path.Length - 1)
            {
                return level[path[i]].Name;
            }

            level = level[path[i]].Branches.ToList();
        }

        return "?";
    }

    private static string ResolveLeafName(IReadOnlyList<DanceBranch> roots, int[] parentPath,
        int leafIndex)
    {
        var level = roots;
        for (var i = 0; i < parentPath.Length; i++)
        {
            if (parentPath[i] >= level.Count)
            {
                return "?";
            }

            if (i == parentPath.Length - 1)
            {
                var leafs = level[parentPath[i]].Leafs.ToList();
                return leafIndex < leafs.Count ? leafs[leafIndex].Name : "?";
            }

            level = level[parentPath[i]].Branches.ToList();
        }

        return "?";
    }
}
