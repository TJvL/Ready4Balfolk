using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Contracts;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What the phone remote can ask for, and what it is told when the answer is no.
/// </summary>
/// <remarks>
/// The hub's commands, not its transport. A SignalR connection needs a live Kestrel and a real
/// client, and what is worth pinning down here is that every refusal comes back as a reason the
/// page can show rather than an exception nobody sees.
/// </remarks>
public sealed class RemoteHubTests : IDisposable
{
    private readonly IQueueService _queueService = Substitute.For<IQueueService>();
    private readonly IQueueConsumptionService _consumption = Substitute.For<IQueueConsumptionService>();
    private readonly IRandomTrackService _randomTracks = Substitute.For<IRandomTrackService>();
    private readonly IDancePool _dancePool = Substitute.For<IDancePool>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private const string Pin = "123456";

    private readonly IEndOfNightAudio _endOfNightAudio = Substitute.For<IEndOfNightAudio>();
    private readonly RemoteAccessService _access = new();
    private readonly IHubContext<RemoteHub> _remoteHubContext = Substitute.For<IHubContext<RemoteHub>>();
    private readonly RemoteConnections _connections;
    private readonly RemoteHub _sut;

    /// <summary>Runs the work where it was asked, which is what the UI thread does in the app.</summary>
    private sealed class ImmediateDispatcher : IRemoteCommandDispatcher
    {
        public Task InvokeAsync(Func<Task> work) => work();

        public Task<T> InvokeAsync<T>(Func<T> work) => Task.FromResult(work());
    }

    private readonly PresentationBroadcaster _broadcaster;

    public RemoteHubTests()
    {
        _connections = new RemoteConnections(_remoteHubContext, _access);
        _settingsStore.Current.Returns(new ApplicationSettings());
        _trackStore.Current.Returns([]);
        _queueService.Enqueue(Arg.Any<IQueueItem>()).Returns(QueueAddResult.Allow());

        // A state the snapshot can be built from: OnConnectedAsync draws the phone straight away,
        // and a bare substitute hands it a null state.
        var presentation = Substitute.For<IPresentationStateService>();
        presentation.Current.Returns(new PresentationState(
            PresentationItem.None, PresentationItem.None, PresentationItem.None, IsPlaying: false));

        _broadcaster = new PresentationBroadcaster(
            presentation,
            _queueService,
            Substitute.For<IHubContext<DisplayHub>>(),
            Substitute.For<IHubContext<RemoteHub>>());

        _sut = new RemoteHub(
            _broadcaster,
            _access,
            _connections,
            new ImmediateDispatcher(),
            _queueService,
            _consumption,
            _endOfNightAudio,
            _randomTracks,
            _dancePool,
            _trackStore,
            _settingsStore);
    }

    // --- Transport controls ---

    [Fact]
    public async Task PlayPause_GoesThroughTheDispatcher()
    {
        // Never straight onto the threadpool thread SignalR handed it: the queue and the audio
        // engine underneath are driven from the UI thread.
        _consumption.PlayPauseAsync().Returns(true);

        var result = await _sut.PlayPause();

        Assert.True(result.Accepted);
        await _consumption.Received(1).PlayPauseAsync();
    }

    /// <summary>
    /// The phone is looking at a screen up to half a second old, so a tap can be about a dance that
    /// has ended: during the moment between two there is nothing loaded, and after the last one
    /// there is nothing at all. It comes back as a screen that has been passed rather than as a tap
    /// that quietly played the finished dance again.
    /// </summary>
    [Fact]
    public async Task PlayPause_AboutADanceThatHasEnded_ComesBackAsAScreenThatMovedOn()
    {
        _consumption.PlayPauseAsync().Returns(false);

        var result = await _sut.PlayPause();

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
    }

    [Fact]
    public async Task Restart_AboutADanceThatHasEnded_ComesBackAsAScreenThatMovedOn()
    {
        _consumption.RestartAsync().Returns(false);

        var result = await _sut.Restart();

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
        await _consumption.Received(1).RestartAsync();
    }

    [Fact]
    public async Task Restart_WhileADanceIsPlaying_IsAccepted()
    {
        _consumption.RestartAsync().Returns(true);

        var result = await _sut.Restart();

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task Skip_OfTheItemThePhoneIsShowing_AdvancesThatItem()
    {
        var showing = new DelayQueueItem(TimeSpan.FromSeconds(30));
        _consumption.CurrentItem.Returns(showing);
        _consumption.AdvanceAsync(showing).Returns(true);

        var result = await _sut.Skip(showing.Id.ToString());

        Assert.True(result.Accepted);
        await _consumption.Received(1).AdvanceAsync(showing);
    }

    /// <summary>A press that lands just after the dance changed is about the one that ended.</summary>
    [Fact]
    public async Task Skip_OfAnItemThatHasEnded_LeavesTheNewOneAlone()
    {
        var ended = new DelayQueueItem(TimeSpan.FromSeconds(30));
        _consumption.CurrentItem.Returns(new DelayQueueItem(TimeSpan.FromSeconds(30)));

        var result = await _sut.Skip(ended.Id.ToString());

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
        await _consumption.DidNotReceiveWithAnyArgs().AdvanceAsync(default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an id")]
    public async Task Skip_NamingNothing_SkipsNothing(string? id)
    {
        _consumption.CurrentItem.Returns(new DelayQueueItem(TimeSpan.FromSeconds(30)));

        var result = await _sut.Skip(id);

        Assert.False(result.Accepted);
        await _consumption.DidNotReceiveWithAnyArgs().AdvanceAsync(default);
    }

    // --- Getting in ---

    /// <summary>
    /// A phone with no good token never gets as far as a command: the connection itself is ended.
    /// </summary>
    /// <remarks>
    /// Refusing the calls one at a time would leave the socket open and the page looking connected,
    /// which is the state the filter exists to clean up after rather than the one to arrive in.
    /// </remarks>
    [Fact]
    public async Task Connecting_WithoutAToken_IsToldSoAndTheConnectionIsEnded()
    {
        _access.Configure(true, "123456");
        var caller = ConnectAs(null);

        await _sut.OnConnectedAsync();

        await caller.Received(1).SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await caller.DidNotReceive().SendCoreAsync(
            DisplayHub.SnapshotMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        _sut.Context.Received(1).Abort();
    }

    /// <summary>Last night's token is not this night's: a changed PIN turns the helper out.</summary>
    [Fact]
    public async Task Connecting_WithATokenFromBeforeThePinChanged_IsEnded()
    {
        _access.Configure(true, "123456");
        var token = _access.TryLogin("123456", "phone").Token;
        _access.Configure(true, "654321");
        var caller = ConnectAs(token);

        await _sut.OnConnectedAsync();

        await caller.Received(1).SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        _sut.Context.Received(1).Abort();
    }

    /// <summary>The legitimate phone is let in and gets both screens to draw from.</summary>
    [Fact]
    public async Task Connecting_WithTheTokenThePinWasExchangedFor_IsLetIn()
    {
        _access.Configure(true, "123456");
        var caller = ConnectAs(_access.TryLogin("123456", "phone").Token);

        await _sut.OnConnectedAsync();

        _sut.Context.DidNotReceive().Abort();
        await caller.DidNotReceive().SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await caller.Received(1).SendCoreAsync(
            DisplayHub.SnapshotMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await caller.Received(1).SendCoreAsync(
            RemoteHub.QueueMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Puts a connection carrying <paramref name="token" /> under the hub, and hands back the
    /// proxy standing in for the phone at the other end of it.
    /// </summary>
    private ISingleClientProxy ConnectAs(string? token)
    {
        _sut.Context = TestData.CreateHubConnection("phone", token);

        var caller = Substitute.For<ISingleClientProxy>();
        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(caller);
        _sut.Clients = clients;

        return caller;
    }

    // --- Queueing ---

    [Fact]
    public async Task QueueMessage_Blank_IsRefusedWithAReason()
    {
        var result = await _sut.QueueMessage("   ");

        Assert.False(result.Accepted);
        Assert.Equal(RemoteRefusal.MessageEmpty, result.Refusal);
        _queueService.DidNotReceive().Enqueue(Arg.Any<IQueueItem>());
    }

    [Fact]
    public async Task QueueMessage_Trimmed_BeforeItIsQueued()
    {
        var result = await _sut.QueueMessage("  last dance  ");

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Is<MessageQueueItem>(item => item.Description == "last dance"));
    }

    [Fact]
    public async Task QueueMessage_AtTheDesktopsLimit_IsQueued()
    {
        var result = await _sut.QueueMessage(new string('a', 60));

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Any<MessageQueueItem>());
    }

    [Fact]
    public async Task QueueMessage_LongerThanTheDesktopDialogAllows_IsRefusedWithAReason()
    {
        // The desktop dialog stops the DJ at 60 characters; the phone talks to this hub directly and
        // is not what enforces that. A caller that skips the page's own textarea limit, or the page's
        // JavaScript entirely, must find the same wall here.
        var result = await _sut.QueueMessage(new string('a', 61));

        Assert.False(result.Accepted);
        Assert.Equal(RemoteRefusal.MessageTooLong, result.Refusal);
        _queueService.DidNotReceive().Enqueue(Arg.Any<IQueueItem>());
    }

    [Fact]
    public async Task QueueTrack_UnknownId_IsRefusedRatherThanThrowing()
    {
        // The library can change under a phone that has been showing a stale search result.
        var result = await _sut.QueueTrack("/music/gone.mp3");

        Assert.False(result.Accepted);
        Assert.Equal(RemoteRefusal.TrackGone, result.Refusal);
    }

    [Fact]
    public async Task QueueTrack_KnownId_IsQueued()
    {
        var track = TestData.CreateTrack();
        _trackStore.Current.Returns([track]);

        var result = await _sut.QueueTrack(track.FileInfo.FullName);

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Any<TrackQueueItem>());
    }

    [Fact]
    public async Task QueueDelay_IsQueuedAsAskedFor()
    {
        var result = await _sut.QueueDelay(120);

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Is<DelayQueueItem>(
            item => item.DelayDuration == TimeSpan.FromMinutes(2)));
    }

    /// <summary>
    /// The phone's own buttons only offer sensible gaps, but the hub is what anybody on the venue
    /// wifi with the PIN actually talks to, so the limits hold here rather than on the page.
    /// </summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-60, 5)]
    [InlineData(4000, 900)]
    public async Task QueueDelay_BeyondWhatAGapCanBe_IsBroughtBackIntoRange(int asked, int queued)
    {
        var result = await _sut.QueueDelay(asked);

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Is<DelayQueueItem>(
            item => item.DelayDuration == TimeSpan.FromSeconds(queued)));
    }

    [Fact]
    public async Task QueueStop_IsQueued()
    {
        var result = await _sut.QueueStop();

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Any<StopQueueItem>());
    }

    [Fact]
    public async Task QueueEndOfNight_TheComputerHasOneChosen_IsQueued()
    {
        _endOfNightAudio.Create().Returns(new EndOfNightQueueItem("last.mp3", TimeSpan.FromMinutes(3)));

        var result = await _sut.QueueEndOfNight();

        Assert.True(result.Accepted);
        _queueService.Received(1).Enqueue(Arg.Any<EndOfNightQueueItem>());
    }

    /// <summary>
    /// Nothing has been chosen at the computer, and there is nothing for the phone to choose from:
    /// somebody stacking chairs is told that rather than left tapping a button that does nothing.
    /// </summary>
    [Fact]
    public async Task QueueEndOfNight_WithNoFileChosen_IsRefusedWithAReason()
    {
        _endOfNightAudio.Create().Returns((EndOfNightQueueItem?)null);

        var result = await _sut.QueueEndOfNight();

        Assert.False(result.Accepted);
        Assert.Equal(RemoteRefusal.NoEndOfNightAudio, result.Refusal);
        _queueService.DidNotReceive().Enqueue(Arg.Any<IQueueItem>());
    }

    [Fact]
    public async Task QueueRandom_NothingToPick_IsRefusedWithAReason()
    {
        _randomTracks.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>()).Returns((Track?)null);

        var result = await _sut.QueueRandom();

        Assert.False(result.Accepted);
        Assert.Equal(RemoteRefusal.NoTrackToPick, result.Refusal);
    }

    [Fact]
    public async Task Enqueue_TheGuardRefuses_HandsBackTheGuardsOwnReason()
    {
        // The phone shows what the queue said, not a generic failure: "the queue would run past the
        // cutoff" is actionable and "something went wrong" is not.
        _queueService.Enqueue(Arg.Any<IQueueItem>())
            .Returns(QueueAddResult.Deny("The queue would run past the cutoff"));

        var result = await _sut.QueueMessage("anything");

        Assert.False(result.Accepted);
        Assert.Equal("The queue would run past the cutoff", result.Reason);
        Assert.Null(result.Refusal);
    }

    /// <summary>
    /// The hub's own refusals are a code the phone page words, never words written here: those were
    /// English, and a Dutch phone read them between the queue guard's Dutch ones.
    /// </summary>
    [Fact]
    public async Task TheHubsOwnRefusal_IsACodeRatherThanWords()
    {
        var result = await _sut.QueueMessage("   ");

        Assert.False(result.Accepted);
        Assert.False(result.QueueChanged);
        Assert.Null(result.Reason);
        Assert.NotNull(result.Refusal);
    }

    // --- Rearranging ---

    /// <summary>The queue behind the hub, holding whatever a test put in it.</summary>
    /// <remarks>
    /// The real service answers about rows, not positions, so the substitute does too: a mock that
    /// returns a refusal for a row it was never given would be a contract nobody implements.
    /// </remarks>
    private void QueueHolds(params IQueueItem[] items)
    {
        _queueService.Items.Returns(items);
        _queueService.IndexOf(Arg.Any<QueueItemId>())
            .Returns(call => Array.FindIndex(items, item => item.Id == call.Arg<QueueItemId>()));
        _queueService.Move(Arg.Any<QueueItemId>(), Arg.Any<int>()).Returns(call =>
        {
            var index = Array.FindIndex(items, item => item.Id == call.ArgAt<QueueItemId>(0));
            var target = call.ArgAt<int>(1);
            return index < 0
                ? QueueChangeResult.Gone
                : target >= 0 && target < items.Length ? QueueChangeResult.Done : QueueChangeResult.Refused;
        });
        _queueService.Remove(Arg.Any<QueueItemId>()).Returns(call =>
            Array.Exists(items, item => item.Id == call.Arg<QueueItemId>())
                ? QueueChangeResult.Done
                : QueueChangeResult.Gone);
    }

    [Fact]
    public async Task MoveUp_FromTheTop_IsRefusedWithAReason()
    {
        var top = new StopQueueItem();
        QueueHolds(top, new StopQueueItem());

        var result = await _sut.MoveUp(top.Id.ToString());

        Assert.False(result.Accepted);
        Assert.False(result.QueueChanged);
        Assert.Equal(RemoteRefusal.CannotMoveUp, result.Refusal);
    }

    [Fact]
    public async Task MoveUp_Elsewhere_MovesTowardsTheFront()
    {
        var third = new StopQueueItem();
        QueueHolds(new StopQueueItem(), new StopQueueItem(), third);

        var result = await _sut.MoveUp(third.Id.ToString());

        Assert.True(result.Accepted);
        _queueService.Received(1).Move(third.Id, 1);
    }

    [Fact]
    public async Task MoveDown_TheQueueRefuses_IsReportedAsARefusal()
    {
        var only = new StopQueueItem();
        QueueHolds(only);

        var result = await _sut.MoveDown(only.Id.ToString());

        Assert.False(result.Accepted);
        Assert.False(result.QueueChanged);
        Assert.Equal(RemoteRefusal.CannotMoveDown, result.Refusal);
    }

    [Fact]
    public async Task MoveUp_TheRowPlayedWhileThePhoneWasLooking_MovesNothing()
    {
        // The list on the phone is up to half a second old, so the top row ending mid-tap is
        // ordinary. Sent as a position, "row two" would move whatever row two had become.
        var played = new StopQueueItem();
        var rest = new StopQueueItem();
        QueueHolds(rest);

        var result = await _sut.MoveUp(played.Id.ToString());

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
        _queueService.DidNotReceive().Move(Arg.Any<QueueItemId>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Remove_TheRowPlayedWhileThePhoneWasLooking_SaysTheQueueMovedOn()
    {
        // Not "connection lost", which is what a failed invoke used to read as, and not a refusal
        // either: the connection is fine and the queue has simply moved past this row.
        var played = new StopQueueItem();
        QueueHolds(new StopQueueItem());

        var result = await _sut.Remove(played.Id.ToString());

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
    }

    [Fact]
    public async Task Remove_TheRowThatWasTapped_IsTheRowThatGoes()
    {
        var wanted = new StopQueueItem();
        QueueHolds(new StopQueueItem(), wanted, new StopQueueItem());

        var result = await _sut.Remove(wanted.Id.ToString());

        Assert.True(result.Accepted);
        _queueService.Received(1).Remove(wanted.Id);
    }

    [Fact]
    public async Task Remove_SomethingThatIsNotARowAtAll_IsRefusedRatherThanThrowing()
    {
        var result = await _sut.Remove("not-an-id");

        Assert.False(result.Accepted);
        Assert.True(result.QueueChanged);
    }

    // --- Search ---

    [Fact]
    public async Task Search_MatchesOnWhatIsTyped()
    {
        _trackStore.Current.Returns([
            TestData.CreateTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre"),
            TestData.CreateTrack(dance: "Scottish", artist: "Someone", title: "Something")
        ]);

        var hits = await _sut.Search("naragonia");

        var hit = Assert.Single(hits);
        Assert.Equal("Naragonia", hit.Artist);
    }

    /// <summary>An empty term is "show me the library", and the cap is what makes that safe.</summary>
    /// <remarks>
    /// A phone screen gets forty rows however large the library is. I first wrote this expecting an
    /// empty term to return nothing; it returns everything, capped, which is the more useful answer
    /// for somebody scrolling rather than typing.
    /// </remarks>
    [Fact]
    public async Task Search_NoTerm_ReturnsTheLibraryCappedAtWhatAPhoneCanShow()
    {
        _trackStore.Current.Returns([.. Enumerable.Range(0, 100)
            .Select(index => TestData.CreateTrack(title: $"Track {index}"))]);

        Assert.Equal(40, (await _sut.Search(null)).Count);
        Assert.Equal(40, (await _sut.Search("   ")).Count);
    }

    [Fact]
    public async Task Search_ManyMatches_IsCappedToo()
    {
        _trackStore.Current.Returns([.. Enumerable.Range(0, 100)
            .Select(index => TestData.CreateTrack(artist: "Naragonia", title: $"Track {index}"))]);

        Assert.Equal(40, (await _sut.Search("naragonia")).Count);
    }

    // --- The socket itself ---

    [Fact]
    public async Task OnConnectedAsync_RemembersTheSocket_SoANewPinCanCloseIt()
    {
        // Letting the phone in is only half of it. Nothing else knows the socket exists, so a PIN
        // change reaches the commands this phone sends and not the queue it is being pushed.
        var phone = await LetInAsync("phone");

        _access.Configure(true, "654321");
        await _connections.TurnOutStaleAsync();

        phone.Received(1).Abort();
    }

    [Fact]
    public async Task OnDisconnectedAsync_ForgetsTheSocket()
    {
        var phone = await LetInAsync("phone");

        await _sut.OnDisconnectedAsync(null);

        _access.Configure(true, "654321");
        await _connections.TurnOutStaleAsync();

        phone.DidNotReceive().Abort();
    }

    [Fact]
    public async Task OnConnectedAsync_APhoneThatWasRefused_IsNeverRemembered()
    {
        // Refused connections are aborted on the spot, and a second abort on a socket that is
        // already gone is the turn-out walking over connections it does not own.
        _access.Configure(true, Pin);
        var refused = TestData.CreateHubConnection("phone", "not-a-token");
        _sut.Context = refused;
        _sut.Clients = Callers();

        await _sut.OnConnectedAsync();
        refused.ClearReceivedCalls();

        _access.Configure(true, "654321");
        await _connections.TurnOutStaleAsync();

        refused.DidNotReceive().Abort();
    }

    /// <summary>A phone that logged in with the current PIN and got past <c>OnConnectedAsync</c>.</summary>
    private async Task<HubCallerContext> LetInAsync(string connectionId)
    {
        _access.Configure(true, Pin);
        var token = _access.TryLogin(Pin, "192.168.1.50").Token;
        Assert.NotNull(token);

        var context = TestData.CreateHubConnection(connectionId, token);
        _sut.Context = context;
        _sut.Clients = Callers();

        await _sut.OnConnectedAsync();
        return context;
    }

    private static IHubCallerClients Callers()
    {
        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(Substitute.For<ISingleClientProxy>());
        return clients;
    }

    public void Dispose()
    {
        _sut.Dispose();
        _broadcaster.Dispose();
    }
}
