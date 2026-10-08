using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What a new PIN does to the phones that are already connected.
/// </summary>
/// <remarks>
/// The token check happens on the way in and on every command, so a turned-out helper can no longer
/// change anything. Watching is the part that was left: the broadcaster pushes the queue and the
/// current dance to everything connected, and the phone the DJ generated a new PIN to get rid of
/// went on receiving all of it until it happened to press something.
/// </remarks>
public sealed class RemoteConnectionsTests
{
    private const string Pin = "123456";
    private const string Client = "192.168.1.50";

    /// <summary>Well past the moment a told phone has to close its own socket.</summary>
    private static readonly TimeSpan PastTheBackstop = TimeSpan.FromMinutes(1);

    private readonly IHubContext<RemoteHub> _hub = Substitute.For<IHubContext<RemoteHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly RemoteAccessService _access = new();
    private readonly FakeTimeProvider _time = new();

    public RemoteConnectionsTests()
    {
        _hub.Clients.Returns(_clients);
        _access.Configure(true, Pin);
    }

    [Fact]
    public async Task TurnOutStaleAsync_AfterThePinChanged_TellsThePhoneAndThenClosesIt()
    {
        var proxy = ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        // The order is the whole point. A socket that simply dies reads as the hall's wifi
        // dropping, and the page reconnects at it all evening instead of showing the PIN form.
        Received.InOrder(() =>
        {
            proxy.SendCoreAsync(
                RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
            phone.Abort();
        });
    }

    [Fact]
    public async Task TurnOutStaleAsync_LeavesThePageAMomentToCloseItBeforeClosingItFromHere()
    {
        // The send coming back means the notice was handed to the connection, not that it left,
        // and an abort straight behind it could win and drop it. The phone then saw a dead socket,
        // reconnected on the same token and was closed again, and the helper read "Reconnecting"
        // where the PIN form should have been (#334). The page closes the socket itself on reading
        // the notice; the close from here is for a page that never does.
        ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();

        phone.DidNotReceive().Abort();

        _time.Advance(PastTheBackstop);

        phone.Received(1).Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_APageThatClosedItself_IsNotClosedAgain()
    {
        ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));

        _access.Configure(true, "654321");
        using var closedByThePage = new CancellationTokenSource();
        phone.ConnectionAborted.Returns(closedByThePage.Token);
        await sut.TurnOutStaleAsync();

        await closedByThePage.CancelAsync();
        _time.Advance(PastTheBackstop);

        phone.DidNotReceive().Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_WhenTheRemoteIsSwitchedOff_ClosesThePhoneToo()
    {
        var proxy = ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));

        _access.Configure(false, Pin);
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        await proxy.Received(1).SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        phone.Received(1).Abort();
    }

    [Fact]
    public async Task AddAsync_APinChangeThatLandedWhileTheSocketWasOpening_TellsItAndClosesIt()
    {
        // The hub checks the token and then registers the socket, and a new PIN in between drops
        // the tokens and sweeps a register this socket is not in yet. Nothing would ever close it
        // afterwards: it sends nothing, so the check on every command never runs, and it goes on
        // being pushed the queue all evening. That is the phone the PIN was changed to get rid of.
        var proxy = ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();

        _access.Configure(true, "654321");

        Assert.False(await sut.AddAsync(phone));
        _time.Advance(PastTheBackstop);

        Received.InOrder(() =>
        {
            proxy.SendCoreAsync(
                RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
            phone.Abort();
        });
    }

    [Fact]
    public async Task AddAsync_APhoneThatIsStillLetIn_IsKeptAndClosedByTheNextNewPin()
    {
        var phone = Connected("phone");
        ProxyFor("phone");
        var sut = Registry();

        Assert.True(await sut.AddAsync(phone));
        _time.Advance(PastTheBackstop);
        phone.DidNotReceive().Abort();

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        phone.Received(1).Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_APhoneHoldingAGoodToken_IsLeftWhereItIs()
    {
        // Nothing has been invalidated, so the sweep has nothing to close. It reads each token
        // rather than closing what it was handed, which is what lets a socket registered a moment
        // ago go down the same path.
        var proxy = ProxyFor("phone");
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));

        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        await proxy.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        phone.DidNotReceive().Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_APhoneThatHasAlreadyGone_IsNotClosedAgain()
    {
        var phone = Connected("phone");
        var sut = Registry();
        Assert.True(await sut.AddAsync(phone));
        sut.Remove(phone);

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        phone.DidNotReceive().Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_ClosesEveryPhoneRatherThanTheFirst()
    {
        var first = Connected("first");
        var second = Connected("second");
        ProxyFor("first");
        ProxyFor("second");
        var sut = Registry();
        Assert.True(await sut.AddAsync(first));
        Assert.True(await sut.AddAsync(second));

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        first.Received(1).Abort();
        second.Received(1).Abort();
    }

    [Fact]
    public async Task TurnOutStaleAsync_APhoneTheNoticeCouldNotReach_IsClosedAnyway()
    {
        // A phone carried out of the hall's wifi mid-send. Closing is the guarantee and telling is
        // the courtesy, and the connection has already left the register: an abort skipped here is
        // a helper nothing will ever close, because no later PIN change will find them to try
        // again. The second phone is the rest of the sweep, which the first must not take with it.
        var lost = ProxyFor("lost");
        lost.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("the phone left the hall")));
        ProxyFor("still-here");

        var gone = Connected("lost");
        var here = Connected("still-here");
        var sut = Registry();
        Assert.True(await sut.AddAsync(gone));
        Assert.True(await sut.AddAsync(here));

        _access.Configure(true, "654321");
        await sut.TurnOutStaleAsync();
        _time.Advance(PastTheBackstop);

        gone.Received(1).Abort();
        here.Received(1).Abort();
    }

    private RemoteConnections Registry() => new(_hub, _access, _time);

    /// <summary>A connection carrying a token the service really issued for the current PIN.</summary>
    private HubCallerContext Connected(string connectionId)
    {
        var token = _access.TryLogin(Pin, Client).Token;
        Assert.NotNull(token);

        return TestData.CreateHubConnection(connectionId, token);
    }

    private ISingleClientProxy ProxyFor(string connectionId)
    {
        var proxy = Substitute.For<ISingleClientProxy>();
        _clients.Client(connectionId).Returns(proxy);
        return proxy;
    }
}
