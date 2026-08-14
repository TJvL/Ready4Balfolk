namespace Ready4Balfolk.Domain.Services.Tracks;

/// <summary>What the pool has been told about the tags: draw from these, never these.</summary>
public sealed record DancePoolSelection(IReadOnlyList<string> Tags, IReadOnlyList<string> ExcludedTags)
{
    public static readonly DancePoolSelection Everything = new([], []);

    public bool IsEverything => Tags.Count == 0 && ExcludedTags.Count == 0;
}

/// <summary>The tags a random pick draws from.</summary>
/// <remarks>
/// One pool, read by the dance panel, the auto-queue and the phone remote alike, so what the screen
/// says is drawing is what actually draws. It is not persisted: it is a decision about tonight, and
/// a pool silently still in force a fortnight later would be the worst kind of hidden state.
/// </remarks>
public interface IDancePool
{
    /// <summary>The chosen tags. Empty means every dance, which is the state it starts in.</summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>The tags a drawn dance must not carry. An exclusion beats an inclusion.</summary>
    IReadOnlyList<string> ExcludedTags { get; }

    IObservable<DancePoolSelection> Observe();

    /// <summary>The pool as a scope a pick can be made against.</summary>
    RandomSelectionScope Scope { get; }

    /// <summary>Walks a tag through its three states: out, drawn from, never drawn.</summary>
    void Toggle(string tag);

    void Clear();
}
