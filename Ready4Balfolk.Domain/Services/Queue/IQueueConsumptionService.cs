using System.Reactive;
using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

public interface IQueueConsumptionService
{
    IQueueItem? CurrentItem { get; }
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
