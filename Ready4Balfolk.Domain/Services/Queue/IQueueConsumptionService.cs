using System.Reactive;
using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueConsumptionService
{
    IQueueItem? CurrentItem { get; }

    /// <summary>How much of the current item is left to play, or zero when nothing is playing.</summary>
    TimeSpan CurrentItemRemaining { get; }
    IObservable<IQueueItem?> WhenCurrentItemChanged { get; }
    IObservable<TimeSpan> WhenElapsedChanged { get; }
    IObservable<TimeSpan> WhenTotalDurationChanged { get; }
    IObservable<bool> WhenIsPlayingChanged { get; }
    IObservable<Unit> WhenItemCompleted { get; }

    /// <summary>Moves the evening on to the next item.</summary>
    /// <param name="requestedFor">
    /// What the caller decided about, or null to advance whatever happens to be playing. A request
    /// naming an item that is no longer the current one is dropped.
    /// </param>
    /// <returns>False when the request was dropped because the evening had already moved on.</returns>
    Task<bool> AdvanceAsync(IQueueItem? requestedFor = null);
    Task PlayPauseAsync();
    Task RestartAsync();
    Task SeekAsync(TimeSpan position);
    Task ClearAsync();
}
