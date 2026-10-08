using Ready4Balfolk.Domain.Models.Tracks;

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

    /// <summary>The library track this item plays, or null when it plays none.</summary>
    /// <remarks>
    /// A request and an auto-track are the same track wrapped twice, and every rule that asks about
    /// one has to ask about the other: duplicates, a file that moved, what the night records.
    /// </remarks>
    public static Track? LibraryTrackOf(IQueueItem? item) => item switch
    {
        TrackQueueItem request => request.Track,
        AutoTrackQueueItem auto => auto.TrackQueueItem.Track,
        _ => null
    };

    /// <summary>The file this item plays, or null when it plays none.</summary>
    /// <remarks>The library's tracks, and the end of the night, which is a file but not one of them.</remarks>
    public static string? FileOf(IQueueItem? item) => item is EndOfNightQueueItem endOfNight
        ? endOfNight.FilePath
        : LibraryTrackOf(item)?.FileInfo.FullName;
}
