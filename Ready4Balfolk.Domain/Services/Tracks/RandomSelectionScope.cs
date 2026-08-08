namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Where a random pick is allowed to look.</summary>
/// <remarks>
/// There is no separate tree to address: the dance list is the tree, so a category is a path into
/// it and a dance is a slug.
/// </remarks>
public abstract record RandomSelectionScope
{
    public sealed record EntireList : RandomSelectionScope;

    public sealed record Category(int[] Path) : RandomSelectionScope;

    public sealed record SingleDance(string Slug) : RandomSelectionScope;
}
