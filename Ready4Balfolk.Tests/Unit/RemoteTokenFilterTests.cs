using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The check on every command, rather than on the socket it arrives on.
/// </summary>
/// <remarks>
/// A PIN change now closes the sockets it invalidates, so what is left for this filter is the token
/// that runs out under a phone nobody touched: nothing re-applies the settings when a token simply
/// ages past its twelve hours, and this is the only thing that notices.
/// </remarks>
public sealed class RemoteTokenFilterTests : IDisposable
{
    private const string Pin = "123456";

    private readonly RemoteAccessService _access = new();
    private readonly ISingleClientProxy _caller = Substitute.For<ISingleClientProxy>();
    private readonly TestHub _hub = new();

    public RemoteTokenFilterTests()
    {
        _access.Configure(true, Pin);

        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(_caller);
        _hub.Clients = clients;
    }

    [Fact]
    public async Task InvokeMethodAsync_AGoodToken_LetsTheCommandThrough()
    {
        var token = _access.TryLogin(Pin, "192.168.1.50").Token;
        Assert.NotNull(token);
        var sut = new RemoteTokenFilter(_access);
        var ran = false;

        await sut.InvokeMethodAsync(Invocation(token), _ =>
        {
            ran = true;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.True(ran);
    }

    [Fact]
    public async Task InvokeMethodAsync_ATokenThatHasRunOut_TellsThePhoneAndRefuses()
    {
        var token = _access.TryLogin(Pin, "192.168.1.50").Token;
        Assert.NotNull(token);
        var invocation = Invocation(token);
        var sut = new RemoteTokenFilter(_access);

        // Not a PIN change, which closes the socket outright, but the same token simply reaching
        // the end of its life while the phone sat in a pocket.
        _access.Configure(true, "654321");

        await Assert.ThrowsAsync<HubException>(async () =>
            await sut.InvokeMethodAsync(invocation, _ => throw new InvalidOperationException("Ran anyway")));

        // A remote that quietly stops working reads as a crashed application. The page is told, so
        // it can put the PIN form back and say why it is there.
        await _caller.Received(1).SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    private HubInvocationContext Invocation(string token)
    {
        var http = new DefaultHttpContext();
        http.Request.QueryString = QueryString.Create("access_token", token);

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new CarriedHttpContext(http));

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("phone");
        context.Features.Returns(features);

        return new HubInvocationContext(
            context,
            Substitute.For<IServiceProvider>(),
            _hub,
            typeof(TestHub).GetMethod(nameof(TestHub.Command), BindingFlags.Public | BindingFlags.Instance)!,
            []);
    }

    public void Dispose() => _hub.Dispose();

    /// <summary>A hub with one method, which is all the invocation context needs to name.</summary>
    private sealed class TestHub : Hub
    {
        /// <summary>Reaches the caller, the way every real command on the remote hub does.</summary>
        public Task Command() => Clients.Caller.SendAsync("nothing");
    }

    /// <summary>How SignalR hands the opening request through to a live connection.</summary>
    private sealed class CarriedHttpContext(HttpContext context) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
}
