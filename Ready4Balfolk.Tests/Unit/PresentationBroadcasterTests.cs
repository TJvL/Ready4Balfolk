using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using DynamicData;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Hubs;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What every connected browser is told, and how often.
/// </summary>
/// <remarks>
/// The sampling is the point of this class rather than a detail of it: the player reports progress
/// about ten times a second, and a socket pushed at that rate to a phone over hall wifi is the
/// display going stale rather than smooth. Both samples run on a scheduler the test drives, so
/// "twice a second" is asserted rather than waited for.
/// </remarks>
public sealed class PresentationBroadcasterTests : IDisposable
{
    private readonly Subject<PresentationState> _stateChanges = new();
    private readonly Subject<PresentationProgress> _progressChanges = new();
    private readonly SourceList<IQueueItem> _queueItems = new();
    private readonly IPresentationStateService _state = Substitute.For<IPresentationStateService>();
    private readonly IQueueService _queue = Substitute.For<IQueueService>();
    private readonly IClientProxy _displayClients = Substitute.For<IClientProxy>();
    private readonly IClientProxy _remoteClients = Substitute.For<IClientProxy>();
    private readonly HistoricalScheduler _scheduler = new();
    private readonly PresentationBroadcaster _sut;

    private static readonly PresentationState Playing = new(
        new PresentationItem(PresentationItemKind.Track, "Mazurka", "Naragonia", "Salamandre"),
        PresentationItem.None,
        IsPlaying: true);

    public PresentationBroadcasterTests()
    {
        _state.Current.Returns(Playing);
        _state.WhenStateChanged.Returns(_stateChanges);
        _state.WhenProgressChanged.Returns(_progressChanges);
        _queue.Connect().Returns(_queueItems.Connect());
        _queue.Items.Returns(_ => _queueItems.Items.ToList());

        _sut = new PresentationBroadcaster(
            _state, _queue, HubContext(_displayClients), HubContext<RemoteHub>(_remoteClients), _scheduler);
    }

    // --- Before anything is running ---

    [Fact]
    public async Task BeforeStart_NothingIsPushed()
    {
        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_displayClients, 0);
    }

    [Fact]
    public void Latest_IsTheCurrentPicture_ForAPageThatHasJustConnected()
    {
        // A page connecting between two changes has to be drawn straight away, or it sits blank
        // until the next track.
        var latest = _sut.Latest;

        Assert.Equal("Mazurka", latest.Current.Primary);
        Assert.True(latest.IsPlaying);
        Assert.Equal(0, latest.ElapsedSeconds);
    }

    // --- State changes ---

    [Fact]
    public async Task AStateChange_ReachesTheDisplayAndTheRemoteAtOnce()
    {
        // Not sampled: a track change is what the display exists to show, and half a second of it
        // showing the previous track is half a second of a room being told the wrong dance.
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_displayClients, 1);
        await AssertSnapshotsAsync(_remoteClients, 1);
    }

    [Fact]
    public async Task OneHubFailing_DoesNotStopTheOther_NorTheNextChange()
    {
        // Fire and forget, on purpose: a browser that closed its tab must not take the playback
        // loop with it.
        _displayClients.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("the socket has gone"));
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        _stateChanges.OnNext(Playing);
        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_remoteClients, 2);
    }

    // --- Progress, sampled ---

    [Fact]
    public async Task Progress_IsSampledDownToTwiceASecond()
    {
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        foreach (var seconds in new[] { 1, 2, 3, 4, 5 })
        {
            _progressChanges.OnNext(new PresentationProgress(TimeSpan.FromSeconds(seconds), TimeSpan.FromMinutes(3)));
        }

        await AssertSnapshotsAsync(_displayClients, 0);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));
        await AssertSnapshotsAsync(_displayClients, 1);
    }

    [Fact]
    public async Task Progress_TheSampleThatIsSent_IsTheLatestOne()
    {
        // Sampling has to take the newest reading, not the oldest one in the window: the bar is
        // meant to be behind by less than half a second, not ahead of nothing.
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        _progressChanges.OnNext(new PresentationProgress(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(3)));
        _progressChanges.OnNext(new PresentationProgress(TimeSpan.FromSeconds(9), TimeSpan.FromMinutes(3)));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));

        Assert.Equal(9, _sut.Latest.ElapsedSeconds);
    }

    // --- The queue, sampled, and only to the remote ---

    [Fact]
    public async Task QueueChanges_GoToTheRemoteAndNotToTheDisplay()
    {
        // The display draws what is playing and what is next; the whole queue is the remote's
        // business, and pushing it at a projector is bandwidth for nobody.
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        _queueItems.Add(new TrackQueueItem(TestData.CreateTrack(), RandomlyAdded: false));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500));

        await AssertReceivedAsync(_remoteClients, RemoteHub.QueueMethod, 1);
        await AssertReceivedAsync(_displayClients, RemoteHub.QueueMethod, 0);
    }

    [Fact]
    public void QueueSnapshot_NumbersTheRowsAndMarksTheAutomaticOne()
    {
        // The remote sends an index back to move or remove a row, so the numbering is the contract
        // rather than a display detail.
        var track = new TrackQueueItem(TestData.CreateTrack(), RandomlyAdded: false);
        _queueItems.AddRange([track, new AutoTrackQueueItem(track)]);

        var snapshot = _sut.QueueSnapshot;

        Assert.Equal([0, 1], snapshot.Select(entry => entry.Index));
        Assert.Equal([false, true], snapshot.Select(entry => entry.IsAuto));
    }

    // --- Shutting up ---

    [Fact]
    public async Task StopAsync_StopsPushing()
    {
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await _sut.StopAsync(TestContext.Current.CancellationToken);

        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_displayClients, 0);
    }

    [Fact]
    public async Task StopAndStartAgain_PushesOnceRatherThanTwice()
    {
        // The server can be switched off and on again from the settings panel, and a subscription
        // left behind on the first run would double every message on the second.
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        await _sut.StopAsync(TestContext.Current.CancellationToken);
        await _sut.StartAsync(TestContext.Current.CancellationToken);

        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_displayClients, 1);
    }

    [Fact]
    public async Task Dispose_StopsPushing()
    {
        await _sut.StartAsync(TestContext.Current.CancellationToken);
        _sut.Dispose();

        _stateChanges.OnNext(Playing);

        await AssertSnapshotsAsync(_displayClients, 0);
    }

    [Fact]
    public void TheContainerStillBuildsIt()
    {
        // The scheduler is a second constructor rather than an optional parameter, and the server
        // resolves this type at startup. Nothing else would notice the container picking the wrong
        // one until somebody switched the web server on.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        services.AddSingleton(_state);
        services.AddSingleton(_queue);
        services.AddSingleton<PresentationBroadcaster>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<PresentationBroadcaster>());
    }

    // --- Plumbing ---

    private static IHubContext<DisplayHub> HubContext(IClientProxy all) => HubContext<DisplayHub>(all);

    private static IHubContext<THub> HubContext<THub>(IClientProxy all)
        where THub : Hub
    {
        var clients = Substitute.For<IHubClients>();
        clients.All.Returns(all);
        var context = Substitute.For<IHubContext<THub>>();
        context.Clients.Returns(clients);
        return context;
    }

    /// <summary>SendAsync is an extension method, so what a proxy actually receives is this.</summary>
    private static async Task AssertReceivedAsync(IClientProxy proxy, string method, int times) =>
        await proxy.Received(times).SendCoreAsync(method, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());

    private static async Task AssertSnapshotsAsync(IClientProxy proxy, int times) =>
        await AssertReceivedAsync(proxy, DisplayHub.SnapshotMethod, times);

    public void Dispose()
    {
        _sut.Dispose();
        _stateChanges.Dispose();
        _progressChanges.Dispose();
        _queueItems.Dispose();
    }
}
