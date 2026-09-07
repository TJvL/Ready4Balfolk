using System.Net;
using System.Net.NetworkInformation;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// What a running server offers as the addresses to point a phone at, which is what the QR code is
/// handed and what the settings panel prints.
/// </summary>
/// <remarks>
/// The ranking itself is covered by <see cref="Unit.LocalAddressesTests"/>. This is the seam: a
/// listener that really bound, asked what it would put in front of a helper in the hall.
/// </remarks>
public sealed class PresentationWebServerAddressesTests
{
    private static NetworkAdapter Wifi(string address) =>
        new(NetworkInterfaceType.Wireless80211, true, [IPAddress.Parse(address)]);

    /// <summary>A container bridge or a virtual switch: an address, and no router behind it.</summary>
    private static NetworkAdapter Bridge(string address) =>
        new(NetworkInterfaceType.Ethernet, false, [IPAddress.Parse(address)]);

    /// <summary>An ordinary cable, with a router behind it.</summary>
    private static NetworkAdapter Cable(string address) =>
        new(NetworkInterfaceType.Ethernet, true, [IPAddress.Parse(address)]);

    [Fact]
    public async Task AServerThatIsDown_OffersNothing()
    {
        await using var server = await RunningWebServer.StartAsync(Wifi("192.168.1.42"));

        await server.Server.ApplyAsync(
            new WebServerOptions(false, server.Port, false, ""), TestContext.Current.CancellationToken);

        Assert.Empty(server.Server.Addresses);
        Assert.Null(server.Server.BoundPort);
    }

    /// <summary>
    /// The bug: the bridge came first in enumeration order, so that was the address in the code and
    /// the helper's phone reached nothing.
    /// </summary>
    [Fact]
    public async Task TheAddressOffered_LeadsWithTheNetworkThatHasARouter()
    {
        await using var server = await RunningWebServer.StartAsync(
            Bridge("172.17.0.1"), Wifi("192.168.1.42"));

        Assert.Equal(
            [$"http://192.168.1.42:{server.Port}", $"http://172.17.0.1:{server.Port}"],
            server.Server.Addresses);
    }

    /// <summary>
    /// The other half of the bug: with nothing to offer the server used to say localhost, which a
    /// phone reads as itself. Nothing at all is what the dialog and the settings panel need to see
    /// in order to say so out loud.
    /// </summary>
    [Fact]
    public async Task OnNoNetworkAtAll_NothingIsOffered_RatherThanLocalhost()
    {
        await using var server = await RunningWebServer.StartAsync();

        Assert.Empty(server.Server.Addresses);
        Assert.Equal(server.Port, server.Server.BoundPort);
    }

    /// <summary>
    /// The same when the only address on the machine is one a phone cannot use. The old fallback
    /// only fired on an empty list, so a loopback or a link-local address left standing here is a
    /// code that scans and reaches nothing.
    /// </summary>
    [Fact]
    public async Task AnAddressOnlyThisMachineCanUse_IsNotOffered()
    {
        await using var server = await RunningWebServer.StartAsync(
            Cable("127.0.0.1"), Wifi("169.254.13.7"));

        Assert.Empty(server.Server.Addresses);
    }

    /// <summary>
    /// A cable plugged in halfway through the evening changes the answer, and nothing restarts the
    /// listener when it does.
    /// </summary>
    [Fact]
    public async Task ANetworkThatArrivesLater_IsOfferedWithoutARestart()
    {
        var adapters = new List<NetworkAdapter>();
        await using var server = await RunningWebServer.StartAsync(() => adapters);

        Assert.Empty(server.Server.Addresses);

        adapters.Add(Wifi("192.168.1.42"));

        Assert.Equal([$"http://192.168.1.42:{server.Port}"], server.Server.Addresses);
    }
}
