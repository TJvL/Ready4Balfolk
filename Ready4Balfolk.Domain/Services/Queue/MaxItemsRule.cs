using System.Globalization;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class MaxItemsRule(int maxItems) : IQueueRule
{
    public Func<IQueueItem, bool>? GetPreAddRemovalPredicate(IQueueItem newItem, IReadOnlyList<IQueueItem> currentItems)
        => null;

    public QueueRuleVerdict? EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> adjustedItems)
        => adjustedItems.Count >= maxItems
            ? new QueueRuleVerdict(false, string.Format(CultureInfo.CurrentCulture, DomainStrings.MaxItemsRule_QueueFull, maxItems))
            : null;

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        if (currentItems.Count <= maxItems)
        {
            return [];
        }

        var indices = new List<int>();
        for (var i = maxItems; i < currentItems.Count; i++)
        {
            indices.Add(i);
        }

        return indices;
    }

    public bool? CanRemove(IQueueItem item) => null;
    public bool? CanMove(IQueueItem item) => null;
    public bool? CanClear(IReadOnlyList<IQueueItem> currentItems) => null;
}
