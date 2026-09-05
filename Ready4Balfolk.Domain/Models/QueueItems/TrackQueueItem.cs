using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record TrackQueueItem(Track Track, bool RandomlyAdded)
    : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    public string Description => $"{Track.Dance} - {Track.Artist} - {Track.Title}";
    public TimeSpan? Duration => Track.Length;
}
