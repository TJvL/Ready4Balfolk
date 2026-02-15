namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record AutoTrackQueueItem(TrackQueueItem TrackQueueItem) : IQueueItem
{
    public string Description => TrackQueueItem.Description;
    public TimeSpan? Duration => TrackQueueItem.Duration;
    public bool RandomlyAdded => TrackQueueItem.RandomlyAdded;
}
