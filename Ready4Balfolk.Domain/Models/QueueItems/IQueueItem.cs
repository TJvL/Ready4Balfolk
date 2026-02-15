namespace Ready4Balfolk.Domain.Models.QueueItems;

public interface IQueueItem
{
    string Description { get; }
    TimeSpan? Duration { get; }
    bool RandomlyAdded { get; }
}
