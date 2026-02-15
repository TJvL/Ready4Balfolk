using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Editor;

public static class DanceTreeTransforms
{
    public static bool IsLeafNameUnique(
        IReadOnlyList<DanceBranch> roots, string name,
        int[]? excludeParentPath = null, int? excludeLeafIndex = null)
    {
        var normalized = StringNormalizer.Normalize(name);
        foreach (var (leafName, parentPath, leafIndex) in CollectAllLeafNames(roots))
        {
            if (excludeParentPath is not null && excludeLeafIndex is not null
                                              && parentPath.SequenceEqual(excludeParentPath)
                                              && leafIndex == excludeLeafIndex)
            {
                continue;
            }

            if (StringNormalizer.Normalize(leafName) == normalized)
                return false;
        }

        return true;
    }

    public static bool IsBranchNameUnique(
        IReadOnlyList<DanceBranch> roots, string name, int[]? excludePath = null)
    {
        var normalized = StringNormalizer.Normalize(name);
        return CollectAllBranchNames(roots).Where(existing => excludePath is null || !existing.Path.SequenceEqual(excludePath)).All(existing => StringNormalizer.Normalize(existing.Name) != normalized);
    }

    private static string GenerateUniqueLeafName(IReadOnlyList<DanceBranch> roots, string? baseName = null)
    {
        baseName ??= DomainStrings.DanceTreeTransforms_NewDance;
        if (IsLeafNameUnique(roots, baseName))
            return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (IsLeafNameUnique(roots, candidate))
                return candidate;
        }
    }

    private static string GenerateUniqueBranchName(IReadOnlyList<DanceBranch> roots, string? baseName = null)
    {
        baseName ??= DomainStrings.DanceTreeTransforms_NewCategory;
        if (IsBranchNameUnique(roots, baseName))
            return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (IsBranchNameUnique(roots, candidate))
                return candidate;
        }
    }

    private static IEnumerable<(string Name, int[] ParentPath, int LeafIndex)> CollectAllLeafNames(
        IReadOnlyList<DanceBranch> branches, int[]? currentPath = null)
    {
        currentPath ??= [];
        for (var i = 0; i < branches.Count; i++)
        {
            var branch = branches[i];
            int[] branchPath = [.. currentPath, i];
            var leafs = branch.Leafs.ToList();
            for (var j = 0; j < leafs.Count; j++)
                yield return (leafs[j].Name, branchPath, j);
            foreach (var child in CollectAllLeafNames(branch.Branches.ToList(), branchPath))
                yield return child;
        }
    }

    private static IEnumerable<(string Name, int[] Path)> CollectAllBranchNames(
        IReadOnlyList<DanceBranch> branches, int[]? currentPath = null)
    {
        currentPath ??= [];
        for (var i = 0; i < branches.Count; i++)
        {
            int[] path = [.. currentPath, i];
            yield return (branches[i].Name, path);
            foreach (var child in CollectAllBranchNames(branches[i].Branches.ToList(), path))
                yield return child;
        }
    }

    private static DanceBranch[] ReplaceBranchAtDepth(
        IReadOnlyList<DanceBranch> siblings, IReadOnlyList<int> path, int depth,
        Func<DanceBranch, DanceBranch> transform)
    {
        var index = path[depth];
        var result = new DanceBranch[siblings.Count];
        for (var i = 0; i < siblings.Count; i++)
        {
            if (i != index)
            {
                result[i] = siblings[i];
                continue;
            }

            if (depth == path.Count - 1)
            {
                result[i] = transform(siblings[i]);
            }
            else
            {
                var branch = siblings[i];
                var newChildren = ReplaceBranchAtDepth(
                    branch.Branches.ToList(), path, depth + 1, transform);
                result[i] = branch with { Branches = newChildren };
            }
        }

        return result;
    }

    public static IReadOnlyList<DanceBranch> RenameBranch(
        IReadOnlyList<DanceBranch> roots, int[] path, string newName)
        => ReplaceBranchAtDepth(roots, path, 0, b => b with { Name = newName });

    public static IReadOnlyList<DanceBranch> ReweightBranch(
        IReadOnlyList<DanceBranch> roots, int[] path, int newWeight)
        => ReplaceBranchAtDepth(roots, path, 0, b => b with { Weight = newWeight });

    public static IReadOnlyList<DanceBranch> AddBranch(
        IReadOnlyList<DanceBranch> roots, int[] parentPath, string? name = null)
    {
        var newBranch = new DanceBranch { Name = name ?? GenerateUniqueBranchName(roots), Weight = 1 };
        return parentPath.Length == 0
            ? [.. roots, newBranch]
            : ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
            {
                Branches = [.. b.Branches, newBranch]
            });
    }

    public static IReadOnlyList<DanceBranch> DeleteBranch(
        IReadOnlyList<DanceBranch> roots, int[] path)
    {
        switch (path.Length)
        {
            case 0:
                return roots;
            case 1:
                return roots.Where((_, i) => i != path[0]).ToList();
            default:
                break;
        }

        var parentPath = path[..^1];
        var childIndex = path[^1];
        return ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
        {
            Branches = b.Branches.Where((_, i) => i != childIndex).ToList()
        });
    }

    public static IReadOnlyList<DanceBranch> RenameLeaf(
        IReadOnlyList<DanceBranch> roots, int[] parentPath, int leafIndex, string newName)
        => ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
        {
            Leafs = b.Leafs.Select((l, i) => i == leafIndex ? l with { Name = newName } : l).ToList()
        });

    public static IReadOnlyList<DanceBranch> ReweightLeaf(
        IReadOnlyList<DanceBranch> roots, int[] parentPath, int leafIndex, int newWeight)
        => ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
        {
            Leafs = b.Leafs.Select((l, i) => i == leafIndex ? l with { Weight = newWeight } : l).ToList()
        });

    public static IReadOnlyList<DanceBranch> AddLeaf(
        IReadOnlyList<DanceBranch> roots, int[] parentPath, string? name = null)
    {
        if (parentPath.Length == 0)
            return roots;

        var newLeaf = new DanceLeaf(name ?? GenerateUniqueLeafName(roots), 1);
        return ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
        {
            Leafs = [.. b.Leafs, newLeaf]
        });
    }

    public static IReadOnlyList<DanceBranch> DeleteLeaf(
        IReadOnlyList<DanceBranch> roots, int[] parentPath, int leafIndex)
        => ReplaceBranchAtDepth(roots, parentPath, 0, b => b with
        {
            Leafs = b.Leafs.Where((_, i) => i != leafIndex).ToList()
        });
}
