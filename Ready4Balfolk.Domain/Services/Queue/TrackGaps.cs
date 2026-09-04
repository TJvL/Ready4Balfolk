using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>The quiet a DJ asks for between one dance and the next.</summary>
/// <remarks>
/// It is not an item in the queue, which is what makes it worth having: a queue with a delay
/// between every pair of tracks is a queue nobody can read. It is still time the evening spends,
/// so anything that says when the evening will finish has to count it.
/// </remarks>
public static class TrackGaps
{
    /// <summary>Whether a gap goes in front of this, which is only ever before a dance.</summary>
    /// <remarks>
    /// A delay, a stop and a message are already the room being given time, and the end of the
    /// night has nothing after it.
    /// </remarks>
    public static bool IsTrack(IQueueItem? item) => item is TrackQueueItem or AutoTrackQueueItem;

    /// <summary>How much the gaps add between what is playing now and the end of this queue.</summary>
    public static TimeSpan Between(IQueueItem? current, IEnumerable<IQueueItem> queued, TimeSpan gap)
    {
        ArgumentNullException.ThrowIfNull(queued);

        if (gap <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var total = TimeSpan.Zero;
        var previous = current;

        foreach (var item in queued)
        {
            if (IsTrack(previous) && IsTrack(item))
            {
                total += gap;
            }

            previous = item;
        }

        return total;
    }
}
