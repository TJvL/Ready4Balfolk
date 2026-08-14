namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>Where a random pick is allowed to look.</summary>
/// <remarks>
/// A pool of tags, or one named dance. There is nothing else to address: the list is flat, and a
/// tag is the only thing that groups dances.
/// </remarks>
public abstract record RandomSelectionScope
{
    /// <summary>
    /// The dances carrying any of these tags and none of the excluded ones. An empty pool is
    /// everything, which is what makes "no tags chosen" mean the whole list rather than nothing at
    /// all; an exclusion always wins, so "bretagne but never chain" means exactly that.
    /// </summary>
    public sealed record Pool(IReadOnlyList<string> Tags, IReadOnlyList<string>? ExcludedTags = null) : RandomSelectionScope
    {
        public IReadOnlyList<string> ExcludedTags { get; init; } = ExcludedTags ?? [];
    }

    public sealed record SingleDance(string Slug) : RandomSelectionScope;

    /// <summary>Anything at all: the pool with nothing in it.</summary>
    public static RandomSelectionScope EntireList { get; } = new Pool([]);
}
