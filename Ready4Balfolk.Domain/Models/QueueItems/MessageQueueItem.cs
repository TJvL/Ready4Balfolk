namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record MessageQueueItem(string Description, TimeSpan? Duration = null) : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    public bool RandomlyAdded => false;
}
