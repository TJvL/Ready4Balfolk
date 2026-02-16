using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Domain.Services.Queue;

public static class QueueGuardBuilder
{
    public static IQueueGuard FromSettings(
        ApplicationSettings settings,
        Func<IQueueItem?> currentItemProvider,
        IQueueHistoryStore historyStore)
    {
        var rules = new List<IQueueRule>
        {
            new AutoTrackRule(settings.AutoQueueRandomTrack)
        };
        if (!settings.AllowDuplicateTracksInQueue)
        {
            rules.Add(new DuplicateTrackRule(currentItemProvider, historyStore));
        }

        rules.Add(new MaxItemsRule(settings.MaxQueueItems));
        return new QueueGuard(rules);
    }
}
