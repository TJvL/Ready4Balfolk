namespace Ready4Balfolk.Domain.Services.Tracks;

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

    IObservable<IReadOnlyList<string>> Observe();

    /// <summary>The pool as a scope a pick can be made against.</summary>
    RandomSelectionScope Scope { get; }

    void Toggle(string tag);

    void Clear();
}
