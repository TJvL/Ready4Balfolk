namespace Ready4Balfolk.Domain.Services.Tracks;

public abstract record RandomSelectionScope
{
    public sealed record EntireTree : RandomSelectionScope;

    public sealed record Subtree(int[] BranchPath) : RandomSelectionScope;

    public sealed record SingleDance(int[] ParentPath, int LeafIndex) : RandomSelectionScope;
}
