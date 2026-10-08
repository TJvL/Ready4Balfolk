using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Ready4Balfolk.Tests.Helpers;
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

    private readonly FakeTimeProvider _time = new();
    private readonly RemoteAccessService _access;
    private readonly ISingleClientProxy _caller = Substitute.For<ISingleClientProxy>();
    private readonly TestHub _hub = new();

    public RemoteTokenFilterTests()
    {
        _access = new RemoteAccessService(_time);
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
        // the end of its life while the phone sat in a pocket. The PIN and the settings are left
        // exactly as they were, so the clock is the only thing that can have refused it.
        _time.Advance(TimeSpan.FromHours(13));

        await Assert.ThrowsAsync<HubException>(async () =>
            await sut.InvokeMethodAsync(invocation, _ => throw new InvalidOperationException("Ran anyway")));

        // A remote that quietly stops working reads as a crashed application. The page is told, so
        // it can put the PIN form back and say why it is there.
        await _caller.Received(1).SendCoreAsync(
            RemoteHub.TurnedOutMethod, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    private HubInvocationContext Invocation(string token) =>
        new(
            TestData.CreateHubConnection("phone", token),
            Substitute.For<IServiceProvider>(),
            _hub,
            typeof(TestHub).GetMethod(nameof(TestHub.Command), BindingFlags.Public | BindingFlags.Instance)!,
            []);

    public void Dispose() => _hub.Dispose();

    /// <summary>A hub with one method, which is all the invocation context needs to name.</summary>
    private sealed class TestHub : Hub
    {
        /// <summary>Reaches the caller, the way every real command on the remote hub does.</summary>
        public Task Command() => Clients.Caller.SendAsync("nothing");
    }
}
