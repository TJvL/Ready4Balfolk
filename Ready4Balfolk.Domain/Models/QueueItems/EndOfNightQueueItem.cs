using Ready4Balfolk.Domain.Resources;

namespace Ready4Balfolk.Domain.Models.QueueItems;

/// <summary>The music that means stop dancing, find your coat, help stack the chairs.</summary>
/// <remarks>
/// Deliberately not a <see cref="TrackQueueItem"/>. The library is what a person has agreed to, file
/// by file, and this file is not in it: it has no dance, no artist and no title, and it would sit in
/// the review queue forever asking for them. The duration is read when it is queued, so the
/// projected end time stays honest, and is null when the file would not say.
/// </remarks>
public sealed record EndOfNightQueueItem(string FilePath, TimeSpan? Duration) : IQueueItem
{
    public string Description => DomainStrings.EndOfNightQueueItem_Description;
    public bool RandomlyAdded => false;
}
