using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record StopQueueItem : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    public string Description => DomainStrings.StopQueueItem_Description;
    public TimeSpan? Duration => null;
    public bool RandomlyAdded => false;
}
