using System.Globalization;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class MaxItemsRule(int maxItems) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => null;

    // Only an actual track spends a slot. The auto-track is a placeholder for an empty one rather
    // than a request, and a delay, a message, a stop or the end of the night is an instruction to
    // the machine, not a dance the room asked for, so none of them count against the limit or are
    // ever evicted by it.
    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
        => item is TrackQueueItem && adjustedItems.Count(i => i is TrackQueueItem) >= maxItems
            ? new QueueRuleVerdict(false, string.Format(CultureInfo.CurrentCulture, DomainStrings.MaxItemsRule_QueueFull, maxItems))
            : null;

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        var indices = new List<int>();
        var kept = 0;
        for (var i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i] is not TrackQueueItem)
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
