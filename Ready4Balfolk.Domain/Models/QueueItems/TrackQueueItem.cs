using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Models.QueueItems;

public sealed record TrackQueueItem(Track Track, bool RandomlyAdded)
    : IQueueItem
{
    public QueueItemId Id { get; init; } = QueueItemId.New();
    /// <summary>The track as the queue writes it by default, a field it lacks taking its separator.</summary>
    /// <remarks>
    /// What the error bar names when the file will not play, so it reads the way every other surface
    /// writes a track rather than "Mazurka -  - Salamandre" for a track with no artist.
    /// </remarks>
    public string Description => TrackTextTemplate.Render(DisplayTemplates.Default.QueueItem, Track);
    public TimeSpan? Duration => Track.Length;
}
