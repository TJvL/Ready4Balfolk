namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record MessageQueueItem(string Description, TimeSpan? Duration = null) : IQueueItem
{
    public bool RandomlyAdded => false;
}
