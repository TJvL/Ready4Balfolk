namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record StopQueueItem : IQueueItem
{
    public string Description => "Stop";
    public TimeSpan? Duration => null;
    public bool RandomlyAdded => false;
}
