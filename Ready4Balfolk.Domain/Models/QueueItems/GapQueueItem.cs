using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Models.QueueItems;

/// <summary>The moment between one dance and the next, while it is happening.</summary>
/// <remarks>
/// Never in the queue. It is what is playing rather than what is queued: the DJ did not put it
/// there, nothing can be done to it, and it is gone as soon as the next dance starts. It is an item
/// only so that the screens can draw it, because a floor watching a bar say "no track playing" for
/// ten seconds cannot tell a gap from a machine that has stopped.
/// </remarks>
public sealed record GapQueueItem(TimeSpan GapDuration) : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    public string Description => DomainStrings.GapQueueItem_Description;
    public TimeSpan? Duration => GapDuration;
    public bool RandomlyAdded => false;
}
