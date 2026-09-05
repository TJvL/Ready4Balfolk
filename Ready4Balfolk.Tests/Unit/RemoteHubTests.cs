using Microsoft.AspNetCore.SignalR;
using NSubstitute;
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
        _settingsStore.Current.Returns(new ApplicationSettings());
        _trackStore.Current.Returns([]);
        _queueService.Enqueue(Arg.Any<IQueueItem>()).Returns(QueueAddResult.Allow());

        // The real one: it is sealed, so there is nothing to substitute, and the commands under
        // test never ask it anything. Only OnConnectedAsync reads its snapshots.
        _broadcaster = new PresentationBroadcaster(
            Substitute.For<IPresentationStateService>(),
            _queueService,
            Substitute.For<IHubContext<DisplayHub>>(),
            Substitute.For<IHubContext<RemoteHub>>());

        _sut = new RemoteHub(
            _broadcaster,
            new RemoteAccessService(),
            new ImmediateDispatcher(),
            _queueService,
            _consumption,
            Substitute.For<IEndOfNightAudio>(),
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
        await _sut.PlayPause();

        await _consumption.Received(1).PlayPauseAsync();
    }

    [Fact]
    public async Task Skip_AdvancesTheQueue()
    {
        await _sut.Skip();

        await _consumption.Received(1).AdvanceAsync();
    }

    // --- Queueing ---

    [Fact]
    public async Task QueueMessage_Blank_IsRefusedWithAReason()
    {
        var result = await _sut.QueueMessage("   ");

        Assert.False(result.Accepted);
        Assert.NotNull(result.Reason);
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
    public async Task QueueTrack_UnknownId_IsRefusedRatherThanThrowing()
    {
        // The library can change under a phone that has been showing a stale search result.
        var result = await _sut.QueueTrack("/music/gone.mp3");

        Assert.False(result.Accepted);
        Assert.NotNull(result.Reason);
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
    public async Task QueueRandom_NothingToPick_IsRefusedWithAReason()
    {
        _randomTracks.PickRandomTrack(Arg.Any<RandomSelectionScope>(), Arg.Any<bool>()).Returns((Track?)null);

        var result = await _sut.QueueRandom();

        Assert.False(result.Accepted);
        Assert.NotNull(result.Reason);
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
        Assert.NotNull(result.Reason);
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
        Assert.NotNull(result.Reason);
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

    public void Dispose()
    {
        _sut.Dispose();
        _broadcaster.Dispose();
    }
}
