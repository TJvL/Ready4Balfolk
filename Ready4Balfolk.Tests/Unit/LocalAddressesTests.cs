using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Which address ends up in the QR code a helper scans in the hall. A machine at a gig is on the
/// venue wifi, a container bridge and a VPN still up from home all at once, and the wrong pick
/// draws a code that scans perfectly and reaches nothing.
/// </summary>
public sealed class LocalAddressesTests
{
    private const int Port = 8420;

    private static NetworkAdapter Wifi(string address, bool gateway = true) =>
        new(NetworkInterfaceType.Wireless80211, gateway, [IPAddress.Parse(address)]);

    private static NetworkAdapter Cable(string address, bool gateway = true) =>
        new(NetworkInterfaceType.Ethernet, gateway, [IPAddress.Parse(address)]);

    private static NetworkAdapter Tunnel(string address, bool gateway = true) =>
        new(NetworkInterfaceType.Tunnel, gateway, [IPAddress.Parse(address)]);

    /// <summary>A container bridge or a virtual switch: an address, and no router behind it.</summary>
    private static NetworkAdapter Bridge(string address) => Cable(address, gateway: false);

    [Fact]
    public void OneNetwork_IsTheAddress()
    {
        var addresses = LocalAddresses.Reachable([Wifi("192.168.1.42")], Port);

        Assert.Equal(["http://192.168.1.42:8420"], addresses);
    }

    /// <summary>
    /// The bug this exists for: the bridge came first in enumeration order and won, so the code
    /// carried an address that only exists inside this machine.
    /// </summary>
    [Fact]
    public void ABridgeListedFirst_LosesToTheNetworkWithARouter()
    {
        var addresses = LocalAddresses.Reachable([Bridge("172.17.0.1"), Wifi("192.168.1.42")], Port);

        Assert.Equal("http://192.168.1.42:8420", addresses[0]);
        Assert.Equal("http://172.17.0.1:8420", addresses[1]);
    }

    /// <summary>
    /// An adapter the system itself calls a tunnel has a router of its own, so the gateway test
    /// alone would let it win. The phone in the hall cannot reach anything down it.
    /// </summary>
    [Fact]
    public void AnAdapterTheSystemCallsATunnel_LosesToTheNetworkTheHallIsOn()
    {
        var addresses = LocalAddresses.Reachable([Tunnel("10.8.0.6"), Wifi("192.168.1.42")], Port);

        Assert.Equal("http://192.168.1.42:8420", addresses[0]);
        Assert.Equal("http://10.8.0.6:8420", addresses[1]);
    }

    /// <summary>
    /// A hall with an unmanaged switch and static addressing: no DHCP, no router, and a cable that
    /// is the only thing the helper's phone can reach. A tunnel from home does have a gateway, and
    /// scoring the two tests together used to add up to putting it first.
    /// </summary>
    [Fact]
    public void ACableWithNoRouter_StillBeatsATunnelThatHasOne()
    {
        var addresses = LocalAddresses.Reachable(
            [Tunnel("10.8.0.6"), Cable("192.168.50.10", gateway: false)], Port);

        Assert.Equal("http://192.168.50.10:8420", addresses[0]);
        Assert.Equal("http://10.8.0.6:8420", addresses[1]);
    }

    /// <summary>
    /// The limit of the tunnel test, written down rather than assumed away. WireGuard on Windows
    /// presents a virtual adapter and OpenVPN's tap driver an ordinary ethernet one, so a VPN that
    /// has a route of its own is not told apart from a cable here and can come first. All that is
    /// promised is that the network the hall is on is still offered, on the next line down.
    /// </summary>
    [Fact]
    public void AVpnThatDoesNotSayItIsOne_IsNotToldApartFromACable()
    {
        var addresses = LocalAddresses.Reachable([Cable("10.8.0.6"), Wifi("192.168.1.42")], Port);

        Assert.Contains("http://192.168.1.42:8420", addresses);
        Assert.Contains("http://10.8.0.6:8420", addresses);
    }

    /// <summary>Everything is still offered, so the DJ can read the next line down when the pick is wrong.</summary>
    [Fact]
    public void EveryOtherAddress_IsStillOffered()
    {
        var addresses = LocalAddresses.Reachable(
            [Bridge("172.17.0.1"), Tunnel("10.8.0.6"), Cable("192.168.1.42"), Wifi("192.168.1.43")],
            Port);

        Assert.Equal(
            [
                "http://192.168.1.42:8420",
                "http://192.168.1.43:8420",
                "http://172.17.0.1:8420",
                "http://10.8.0.6:8420"
            ],
            addresses);
    }

    /// <summary>An adapter that got no answer from anything gives itself one of these.</summary>
    [Fact]
    public void ALinkLocalAddress_IsNotOffered()
    {
        var addresses = LocalAddresses.Reachable([Wifi("169.254.13.7", gateway: false)], Port);

        Assert.Empty(addresses);
    }

    [Fact]
    public void ALoopbackAddressOnAnOrdinaryAdapter_IsNotOffered()
    {
        var addresses = LocalAddresses.Reachable([Cable("127.0.0.1")], Port);

        Assert.Empty(addresses);
    }

    [Fact]
    public void AnIpv6Address_IsNotOffered()
    {
        var adapter = new NetworkAdapter(
            NetworkInterfaceType.Wireless80211, true, [IPAddress.Parse("fe80::1"), IPAddress.Parse("192.168.1.42")]);

        var addresses = LocalAddresses.Reachable([adapter], Port);

        Assert.Equal(["http://192.168.1.42:8420"], addresses);
    }

    /// <summary>
    /// No network at all. Localhost would draw a code that scans and sends the phone to itself, so
    /// there is nothing here and the dialog says so instead.
    /// </summary>
    [Fact]
    public void NoNetworkAtAll_OffersNothingRatherThanLocalhost()
    {
        var addresses = LocalAddresses.Reachable([], Port);

        Assert.Empty(addresses);
    }

    /// <summary>
    /// Two adapters that rank the same keep the order the operating system gave them: there is
    /// nothing left to tell a cable and the wifi apart, and inventing a tie-break would only make
    /// the pick unstable between evenings.
    /// </summary>
    [Fact]
    public void TwoEquallyGoodNetworks_KeepTheOrderTheSystemGave()
    {
        var addresses = LocalAddresses.Reachable([Cable("192.168.1.42"), Wifi("192.168.1.43")], Port);

        Assert.Equal(["http://192.168.1.42:8420", "http://192.168.1.43:8420"], addresses);
    }

    [Fact]
    public void ThePort_IsTheOneTheServerBound()
    {
        var addresses = LocalAddresses.Reachable([Wifi("192.168.1.42")], 9999);

        Assert.Equal(["http://192.168.1.42:9999"], addresses);
    }

    /// <summary>
    /// The one test that reads the real machine, because everything above hands the adapters in and
    /// so never touches the code that talks to the operating system. Whatever this machine happens
    /// to be on, and it may be on nothing, no address it can only use itself may be offered.
    /// </summary>
    [Fact]
    public void WhateverThisMachineIsOn_NothingOnlyItCanUseIsOffered()
    {
        var addresses = LocalAddresses.Reachable(LocalAddresses.ThisMachine(), Port);

        foreach (var address in addresses)
        {
            Assert.StartsWith("http://", address, StringComparison.Ordinal);
            Assert.EndsWith($":{Port}", address, StringComparison.Ordinal);

            var ip = IPAddress.Parse(address["http://".Length..^$":{Port}".Length]);
            Assert.Equal(AddressFamily.InterNetwork, ip.AddressFamily);
            Assert.False(IPAddress.IsLoopback(ip));

            var bytes = ip.GetAddressBytes();
            Assert.False(bytes[0] == 169 && bytes[1] == 254, $"{ip} is a link-local address.");
        }
    }
}
