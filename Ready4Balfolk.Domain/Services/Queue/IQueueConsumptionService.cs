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

    Task AdvanceAsync();
    Task PlayPauseAsync();
    Task RestartAsync();
    Task SeekAsync(TimeSpan position);
    Task ClearAsync();
}
