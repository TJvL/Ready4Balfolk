using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Web.Contracts;
using Ready4Balfolk.Web.Hubs;

namespace Ready4Balfolk.Web;

/// <summary>Pushes the presentation state out to every connected browser.</summary>
/// <remarks>
/// Also the single source of the "what is on screen now" answer, so a page that connects mid-track
/// gets the real picture rather than an empty one.
/// </remarks>
public sealed class PresentationBroadcaster(
    IPresentationStateService presentationState,
    IQueueService queueService,
    IHubContext<DisplayHub> displayHub,
    IHubContext<RemoteHub> remoteHub) : IHostedService, IDisposable
{
    /// <summary>
    /// Progress arrives about ten times a second, which is far more than a browser drawing a bar and
    /// a m:ss clock can use. Twice a second keeps the bar smooth and the socket quiet.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    private readonly CompositeDisposable _disposables = [];

    private readonly IScheduler _sampleScheduler = Scheduler.Default;

    private volatile PresentationProgress _lastProgress = PresentationProgress.Zero;

    /// <summary>The same broadcaster with the sampling put on a scheduler the caller drives.</summary>
    /// <remarks>
    /// Only tests pass one. Half a second of real time is not something to wait through once per
    /// assertion, and waiting for it is how a test that means "sampled" comes to mean "eventually".
    /// </remarks>
    public PresentationBroadcaster(
        IPresentationStateService presentationState,
        IQueueService queueService,
        IHubContext<DisplayHub> displayHub,
        IHubContext<RemoteHub> remoteHub,
        IScheduler sampleScheduler)
        : this(presentationState, queueService, displayHub, remoteHub)
    {
        _sampleScheduler = sampleScheduler;
    }

    /// <summary>The current picture, for a page that has just connected.</summary>
    public PresentationSnapshotDto Latest =>
        PresentationSnapshotDto.From(presentationState.Current, _lastProgress);

    /// <summary>The queue as the remote lists it.</summary>
    public IReadOnlyList<QueueEntryDto> QueueSnapshot =>
        queueService.Items.Select(QueueEntryDto.From).ToList();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _disposables.Add(presentationState.WhenStateChanged
            .Subscribe(_ => BroadcastSnapshot()));

        _disposables.Add(presentationState.WhenProgressChanged
            .Sample(ProgressInterval, _sampleScheduler)
            .Subscribe(progress =>
            {
                _lastProgress = progress;
                BroadcastSnapshot();
            }));

        _disposables.Add(queueService.Connect()
            .Sample(ProgressInterval, _sampleScheduler)
            .Subscribe(_ => BroadcastQueue()));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _disposables.Clear();
        return Task.CompletedTask;
    }

    private void BroadcastSnapshot()
    {
        var snapshot = Latest;
        // Fire and forget on purpose: a disconnecting client must not stop the app's playback loop,
        // and SignalR already drops sends to a socket that has gone.
        _ = displayHub.Clients.All.SendAsync(DisplayHub.SnapshotMethod, snapshot);
        _ = remoteHub.Clients.All.SendAsync(DisplayHub.SnapshotMethod, snapshot);
    }

    private void BroadcastQueue() =>
        _ = remoteHub.Clients.All.SendAsync(RemoteHub.QueueMethod, QueueSnapshot);

    public void Dispose() => _disposables.Dispose();
}
