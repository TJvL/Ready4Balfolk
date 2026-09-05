namespace Ready4Balfolk.Domain.Models.QueueItems;

/// <summary>Which items are a file playing in the hall, and which are the evening around one.</summary>
/// <remarks>
/// What a transport command acts on. A delay, a message, a stop and the moment between two dances
/// are all the room being given time: there is no stream behind any of them to play, pause, start
/// again or move through. The music that ends the night is a file like every dance, so it is one of
/// these even though it is not in the library.
/// </remarks>
public static class AudioItems
{
    /// <summary>Whether this is sound the DJ can act on.</summary>
    public static bool IsAudio(IQueueItem? item) =>
        item is TrackQueueItem or AutoTrackQueueItem or EndOfNightQueueItem;
}
