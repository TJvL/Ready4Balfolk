using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record DelayQueueItem(TimeSpan DelayDuration) : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    public string Description => DomainStrings.DelayQueueItem_Description;
    public TimeSpan? Duration => DelayDuration;
    public bool RandomlyAdded => false;
}
