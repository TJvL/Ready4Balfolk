using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Services.Queue;

public sealed class QueueGuard(IEnumerable<IQueueRule> rules) : IQueueGuard
{
    public QueueAddResult EvaluateAdd(IQueueItem item, IReadOnlyList<IQueueItem> currentItems)
    {
        // Phase 1: collect removal predicates
        var predicates = new List<Func<IQueueItem, bool>>();
        foreach (var rule in rules)
        {
            var predicate = rule.GetPreAddRemovalPredicate(item, currentItems);
            if (predicate is not null)
            {
                predicates.Add(predicate);
            }
        }

        // Compute adjusted list
        Func<IQueueItem, bool>? combinedPredicate = null;
        IReadOnlyList<IQueueItem> adjustedItems;
        if (predicates.Count > 0)
        {
            combinedPredicate = i => predicates.Exists(p => p(i));
            adjustedItems = currentItems.Where(i => !combinedPredicate(i)).ToList();
        }
        else
        {
            adjustedItems = currentItems;
        }

        // Phase 2: evaluate add rules — first deny wins
        foreach (var rule in rules)
        {
            var verdict = rule.EvaluateAdd(item, adjustedItems);
            if (verdict is { Allowed: false })
            {
                return QueueAddResult.Deny(verdict.Reason ?? DomainStrings.QueueGuard_DeniedByRule);
            }
        }

        return QueueAddResult.Allow(combinedPredicate);
    }

    public IReadOnlyList<int> GetEvictionIndices(IReadOnlyList<IQueueItem> currentItems)
    {
        var allIndices = new HashSet<int>();
        foreach (var rule in rules)
        {
            foreach (var index in rule.GetEvictionIndices(currentItems))
            {
                allIndices.Add(index);
            }
        }

        var sorted = allIndices.ToList();
        sorted.Sort();
        sorted.Reverse();
        return sorted;
    }

    public bool CanRemove(IQueueItem item)
    {
        foreach (var rule in rules)
        {
            var result = rule.CanRemove(item);
            if (result.HasValue)
            {
                return result.Value;
            }
        }

        return true;
    }

    public bool CanMove(IQueueItem item)
    {
        foreach (var rule in rules)
        {
            var result = rule.CanMove(item);
            if (result.HasValue)
            {
                return result.Value;
            }
        }

        return true;
    }

    public bool CanClear(IReadOnlyList<IQueueItem> currentItems)
    {
        foreach (var rule in rules)
        {
            var result = rule.CanClear(currentItems);
            if (result.HasValue)
            {
                return result.Value;
            }
        }

        return true;
    }
}
