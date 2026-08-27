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

    [Fact]
    public async Task MoveUp_FromTheTop_IsRefusedWithoutAskingTheQueue()
    {
        var result = await _sut.MoveUp(0);

        Assert.False(result.Accepted);
        _queueService.DidNotReceive().Move(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task MoveUp_Elsewhere_MovesTowardsTheFront()
    {
        _queueService.Move(2, 1).Returns(true);

        var result = await _sut.MoveUp(2);

        Assert.True(result.Accepted);
        _queueService.Received(1).Move(2, 1);
    }

    [Fact]
    public async Task MoveDown_TheQueueRefuses_IsReportedAsARefusal()
    {
        _queueService.Move(Arg.Any<int>(), Arg.Any<int>()).Returns(false);

        var result = await _sut.MoveDown(0);

        Assert.False(result.Accepted);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task RemoveAt_AlreadyGone_IsRefusedRatherThanThrowing()
    {
        // Two phones on the same queue is the normal case, so an index can go stale mid-tap.
        _queueService.RemoveAt(Arg.Any<int>()).Returns(false);

        var result = await _sut.RemoveAt(3);

        Assert.False(result.Accepted);
        Assert.NotNull(result.Reason);
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
