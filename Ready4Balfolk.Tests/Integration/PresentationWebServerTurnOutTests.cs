using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Time.Testing;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Contracts;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// What a new PIN does to the sockets that are already open, against a server that really bound a
/// port and clients that really connected.
/// </summary>
/// <remarks>
/// The DJ generates a new PIN because somebody is to be turned out, and dropping the tokens only
/// stops that phone sending: the queue and the current dance went on arriving on its screen. The
/// other half of the same evening is the projector, which has no PIN by design, and a fix that
/// closed everything connected would put the hall's screen out because somebody changed a PIN.
/// </remarks>
public sealed class PresentationWebServerTurnOutTests
{
    private const string Pin = "111111";

    /// <summary>How long a client is given to notice something the server did to its socket.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ANewPin_ClosesThePhoneAndTellsItWhy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await RunningWebServer.StartAsync();
        await RemoteWithPinAsync(server, Pin, cancellationToken);

        await using var phone = await ConnectedPhoneAsync(server, Pin, cancellationToken);

        var told = new TaskCompletionSource();
        var closed = new TaskCompletionSource();
        phone.On(RemoteHub.TurnedOutMethod, () => { told.TrySetResult(); });
        phone.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await RemoteWithPinAsync(server, "222222", cancellationToken);

        // Told, and only then closed. A socket that simply dies is the hall's wifi as far as the
        // phone can tell, and the page reconnects at it rather than showing the PIN form.
        await told.Task.WaitAsync(Patience, cancellationToken);
        await closed.Task.WaitAsync(Patience, cancellationToken);
    }

    [Fact]
    public async Task ANewPin_LeavesTheDisplayWhereItIs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await RunningWebServer.StartAsync();
        await RemoteWithPinAsync(server, Pin, cancellationToken);

        await using var projector = Building($"http://127.0.0.1:{server.Port}/hubs/display");
        var projectorClosed = false;
        projector.Closed += _ =>
        {
            projectorClosed = true;
            return Task.CompletedTask;
        };
        await projector.StartAsync(cancellationToken);

        await using var phone = await ConnectedPhoneAsync(server, Pin, cancellationToken);
        var closed = new TaskCompletionSource();
        phone.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await RemoteWithPinAsync(server, "222222", cancellationToken);
        await closed.Task.WaitAsync(Patience, cancellationToken);

        // The display page has no PIN and never did: it is the screen in the corner of the hall
        // and anybody on the network is meant to be able to open it. The shortest way to close the
        // phones would have been to make a new PIN restart the listener, which takes the hall's
        // screen down with it; waiting for the phone to go first is what makes this an assertion
        // rather than a race the display usually wins.
        Assert.False(projectorClosed, "The projector was dropped by a PIN change.");
        Assert.Equal(HubConnectionState.Connected, projector.State);
    }

    [Fact]
    public async Task ThePinBeingReapplied_LeavesThePhoneWhereItIs()
    {
        // Every settings save re-applies the whole set of options, most of them about something
        // else entirely. Turning the helpers out on each one would make the remote unusable.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await RunningWebServer.StartAsync();
        await RemoteWithPinAsync(server, Pin, cancellationToken);

        await using var phone = await ConnectedPhoneAsync(server, Pin, cancellationToken);

        await RemoteWithPinAsync(server, Pin, cancellationToken);
        await RemoteWithPinAsync(server, Pin, cancellationToken);

        // Asked over the socket rather than read off the client's own state, because closing one
        // is asynchronous and a phone that has just been turned out still says Connected for a
        // moment. A command that comes back answered is a socket that is open and a token the
        // server still honours, both at once.
        Assert.False(await RefusedAsync(phone, cancellationToken), "The re-applied PIN turned the phone out.");
        Assert.Equal(WebServerState.Running, server.Server.State);
    }

    [Fact]
    public async Task ATokenThatAgedOut_RefusesTheCommandsOfAPhoneNobodyClosed()
    {
        // The other way a token stops being good. Nothing re-applies the settings when twelve
        // hours simply pass, so no socket is closed and the phone sits there looking connected:
        // the check on every command is the only thing that notices, and it only runs because the
        // hub is built with the filter on it.
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider();
        await using var server = await RunningWebServer.StartAsync(clock);
        await RemoteWithPinAsync(server, Pin, cancellationToken);

        await using var phone = await ConnectedPhoneAsync(server, Pin, cancellationToken);

        var told = new TaskCompletionSource();
        phone.On(RemoteHub.TurnedOutMethod, () => { told.TrySetResult(); });

        clock.Advance(TimeSpan.FromHours(13));

        Assert.True(await RefusedAsync(phone, cancellationToken), "A token from last night still worked.");
        await told.Task.WaitAsync(Patience, cancellationToken);
        Assert.Equal(HubConnectionState.Connected, phone.State);
    }

    /// <summary>Sends the smallest command the page has and says whether the server refused it.</summary>
    /// <remarks>
    /// An empty message, which the hub answers out of hand: it reaches neither the queue nor the
    /// library, so the only thing that can stop it is the token check in front of every command.
    /// </remarks>
    private static async Task<bool> RefusedAsync(HubConnection phone, CancellationToken cancellationToken)
    {
        try
        {
            await phone.InvokeAsync<CommandResultDto>(nameof(RemoteHub.QueueMessage), "", cancellationToken);
            return false;
        }
        catch (HubException)
        {
            return true;
        }
    }

    private static Task RemoteWithPinAsync(RunningWebServer server, string pin, CancellationToken cancellationToken) =>
        server.Server.ApplyAsync(new WebServerOptions(true, server.Port, true, pin), cancellationToken);

    private static async Task<HubConnection> ConnectedPhoneAsync(
        RunningWebServer server, string pin, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        using var response = await client.PostAsJsonAsync(
            $"http://127.0.0.1:{server.Port}/api/remote/login", new RemoteLoginRequest(pin), cancellationToken);

        var login = await response.Content.ReadFromJsonAsync<RemoteLoginResult>(cancellationToken);
        Assert.NotNull(login?.Token);

        var connection = Building(
            $"http://127.0.0.1:{server.Port}/hubs/remote?access_token={Uri.EscapeDataString(login.Token)}");
        await connection.StartAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// A client with no automatic reconnect, so a socket the server closed stays closed.
    /// </summary>
    /// <remarks>
    /// The pages do reconnect, which is exactly why they are told before they are closed. Here it
    /// would only put the connection back and hide what is being asserted.
    /// </remarks>
    private static HubConnection Building(string url) =>
        new HubConnectionBuilder().WithUrl(url).Build();
}
