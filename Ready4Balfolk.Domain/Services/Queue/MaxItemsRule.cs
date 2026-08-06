using System.Globalization;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class MaxItemsRule(int maxItems) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => null;

    // The auto-track is a placeholder for an empty slot rather than a request, so it never counts
    // against the limit and is never evicted by it. Without this the limit would either push the
    // auto-track out of the queue or cost the user a request slot.
    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
        => item is not AutoTrackQueueItem && adjustedItems.Count(i => i is not AutoTrackQueueItem) >= maxItems
            ? new QueueRuleVerdict(false, string.Format(CultureInfo.CurrentCulture, DomainStrings.MaxItemsRule_QueueFull, maxItems))
            : null;

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        var indices = new List<int>();
        var kept = 0;
        for (var i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i] is AutoTrackQueueItem)
            {
                continue;
            }

            kept++;
            if (kept > maxItems)
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    public bool? CanRemove(IQueueItem item) => null;
    public bool? CanMove(IQueueItem item) => null;
    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems) => null;
}
