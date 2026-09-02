using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Domain.Services.Queue;

public static class QueueGuardBuilder
{
    public static IQueueGuard FromSettings(
        ApplicationSettings settings,
        Func<IQueueItem?> currentItemProvider,
        IQueueHistoryStore historyStore,
        Func<TimeSpan> currentItemRemainingProvider,
        TimeProvider time)
    {
        var rules = new List<IQueueRule>
        {
            // First: once the evening has been declared over, no other rule has an opinion worth
            // hearing, and its refusal is the one that explains what happened.
            new EndOfNightRule(currentItemProvider),
            new AutoTrackRule(settings.AutoQueueRandomTrack)
        };
        if (!settings.AllowDuplicateTracksInQueue)
        {
            rules.Add(new DuplicateTrackRule(currentItemProvider, historyStore));
        }

        if (settings.QueueCutoffEnabled)
        {
            rules.Add(new QueueCutoffRule(settings.QueueCutoff, settings.QueueCutoffGrace,
                currentItemRemainingProvider, () => time.GetLocalNow().DateTime));
        }

        rules.Add(new MaxItemsRule(settings.MaxQueueItems));
        return new QueueGuard(rules);
    }
}
