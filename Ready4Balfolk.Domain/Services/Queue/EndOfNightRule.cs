using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>
/// Closes the queue once the end of the night is in it: not a track, not a request, not a delay or a
/// message, and not another one of itself.
/// </summary>
/// <remarks>
/// It is not an entry that happens to be last, it is the evening being declared over, and everything
/// after that point is somebody carrying speakers to a car. The auto-track goes with it, or the
/// machine would extend an evening the user has just ended, and removing the entry reopens the
/// evening, because changing your mind is not an error state.
/// </remarks>
public sealed class EndOfNightRule(Func<IQueueItem?> currentItemProvider) : IQueueRule
{
    // Queued last of all, with the auto-track taken out from under it: the auto-track sits at the
    // tail of the queue, and this is the one thing that belongs after it.
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => newItem is EndOfNightQueueItem ? item => item is AutoTrackQueueItem : null;

    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
        => HasEnded(adjustedItems)
            ? new QueueRuleVerdict(false, DomainStrings.EndOfNightRule_EveningEnded, QueueDenial.EveningEnded)
            : null;

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems) => [];

    public bool? CanRemove(IQueueItem item) => null;

    // Moving it up would leave the evening running after the thing that ended it.
    public bool? CanMove(IQueueItem item) => item is EndOfNightQueueItem ? false : null;

    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems) => null;

    /// <summary>Whether the evening has been called, whether it is queued or already playing.</summary>
    /// <remarks>
    /// The playing case matters as much as the queued one: without it the queue would reopen the
    /// moment the closing music started, and the auto-queue would put a dance behind it.
    /// </remarks>
    private bool HasEnded(IReadOnlyList<IQueueItem> items) =>
        items.Any(i => i is EndOfNightQueueItem) || currentItemProvider() is EndOfNightQueueItem;
}
