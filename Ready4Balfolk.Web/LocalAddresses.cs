using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ready4Balfolk.Web;

/// <summary>One adapter, reduced to the little a choice between addresses can be made on.</summary>
/// <param name="Kind">What the operating system calls the adapter.</param>
/// <param name="HasDefaultGateway">Whether the adapter was given a router to send traffic to.</param>
/// <param name="Addresses">The adapter's own addresses, in the order the operating system lists them.</param>
public sealed record NetworkAdapter(
    NetworkInterfaceType Kind, bool HasDefaultGateway, IReadOnlyList<IPAddress> Addresses);

/// <summary>Which of this machine's addresses a phone in the same hall stands a chance of reaching.</summary>
/// <remarks>
/// <para>
/// A machine at a gig is on more than one network at once: the hall's wifi, a cable, a container
/// bridge, a VPN still up from home. Enumeration order picks between them by accident, and the
/// accident is what ends up in the QR code a helper scans, so the order is decided here instead.
/// </para>
/// <para>
/// There are two signals to decide it on, and neither is strong. A container bridge or a virtual
/// switch is handed no router, so an adapter with a default gateway goes ahead of one without.
/// An adapter the operating system itself types as a tunnel or a PPP link goes behind everything
/// else, gateway or not.
/// </para>
/// <para>
/// What that does not catch is a userspace VPN, and the rule should not be read as if it did.
/// WireGuard on Windows presents a Wintun adapter, which reports a virtual interface type rather
/// than a tunnel; OpenVPN's tap-windows6 presents an ordinary ethernet adapter; a tun device on
/// Linux does not reliably type as a tunnel either. Any of those, with a route of its own, is
/// indistinguishable from a cable here and can still come first. That is why every other address
/// is still offered and printed under the code: the DJ reads the next line down rather than being
/// stuck with the one guess.
/// </para>
/// </remarks>
public static class LocalAddresses
{
    /// <summary>
    /// This machine's adapters as they are right now, read from the operating system.
    /// </summary>
    /// <remarks>
    /// Adapters come and go during an evening: a dongle is pulled out, wifi is joined halfway
    /// through. Reading one that disappeared between the listing and the read throws, and this is
    /// called from a status update, so a vanished adapter is dropped rather than allowed to
    /// surface as a failure notice in front of a room.
    /// </remarks>
    public static IReadOnlyList<NetworkAdapter> ThisMachine()
    {
        try
        {
            return Describe(NetworkInterface.GetAllNetworkInterfaces());
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    /// <summary>Every address worth offering, best guess first.</summary>
    public static IReadOnlyList<string> Reachable(IEnumerable<NetworkAdapter> adapters, int port)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        return
        [
            .. BestFirst(adapters)
                .SelectMany(adapter => adapter.Addresses.Where(CanBeReachedFromAnotherDevice))
                .Select(address => $"http://{address}:{port}")
        ];
    }

    /// <summary>The adapters that are up and are not this machine talking to itself.</summary>
    private static List<NetworkAdapter> Describe(IEnumerable<NetworkInterface> interfaces)
    {
        var adapters = new List<NetworkAdapter>();
        foreach (var nic in interfaces)
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            try
            {
                var properties = nic.GetIPProperties();
                adapters.Add(new NetworkAdapter(
                    nic.NetworkInterfaceType,
                    properties.GatewayAddresses.Any(gateway => IsRouter(gateway.Address)),
                    [.. properties.UnicastAddresses.Select(unicast => unicast.Address)]));
            }
            catch (NetworkInformationException)
            {
                // The adapter went away between being listed and being read. One address fewer to
                // offer, which is a great deal better than an error over the evening.
            }
        }

        return adapters;
    }

    /// <summary>
    /// Best guess first: never an adapter the system calls a tunnel ahead of one it does not, and
    /// among those the ones that were given a router.
    /// </summary>
    /// <remarks>
    /// Tunnel-ness is the first key rather than part of a score, because the network the hall is
    /// on does not always have a router. An unmanaged switch and a static address is a real venue,
    /// and that cable has to stay ahead of a tunnel that does have a gateway of its own. Ordering
    /// is stable, so adapters that tie keep the order the operating system listed them in.
    /// </remarks>
    private static IEnumerable<NetworkAdapter> BestFirst(IEnumerable<NetworkAdapter> adapters) =>
        adapters
            .OrderBy(adapter => IsTunnel(adapter.Kind) ? 1 : 0)
            .ThenBy(adapter => adapter.HasDefaultGateway ? 0 : 1);

    /// <summary>
    /// What the operating system itself types as a tunnel, or as a PPP link. A userspace VPN does
    /// not have to report either and mostly does not, which is the limit written out at the top of
    /// this file.
    /// </summary>
    private static bool IsTunnel(NetworkInterfaceType kind) =>
        kind is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp;

    /// <summary>An all-zero gateway is the placeholder for having been given none.</summary>
    private static bool IsRouter(IPAddress? gateway) =>
        gateway is not null && !gateway.Equals(IPAddress.Any) && !gateway.Equals(IPAddress.IPv6Any);

    /// <summary>
    /// IPv4 only, and nothing that only this machine can use: a phone told 127.0.0.1 dials itself,
    /// and 169.254.x.x is what an adapter gives itself when no network answered at all.
    /// </summary>
    private static bool CanBeReachedFromAnotherDevice(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] != 169 || bytes[1] != 254;
    }
}
