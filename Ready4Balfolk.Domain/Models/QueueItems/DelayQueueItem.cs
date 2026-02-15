namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record DelayQueueItem(TimeSpan DelayDuration) : IQueueItem
{
    public string Description => "Delay";
    public TimeSpan? Duration => DelayDuration;
    public bool RandomlyAdded => false;
}
