namespace Ready4Balfolk.Domain.Models.QueueItems;

public interface IQueueItem
{
    /// <summary>What this row is called while it is queued. Never its position.</summary>
    QueueItemId Id { get; }

    string Description { get; }
    TimeSpan? Duration { get; }
    bool RandomlyAdded { get; }
}
