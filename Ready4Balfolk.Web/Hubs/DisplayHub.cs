using Microsoft.AspNetCore.SignalR;

namespace Ready4Balfolk.Web.Hubs;

/// <summary>The display page's connection. Read only, and deliberately so.</summary>
/// <remarks>
/// It has no command methods and needs no PIN, so a display left open in the corner of a hall is
/// never a way in. Everything that can change something lives on <see cref="RemoteHub"/>.
/// </remarks>
public sealed class DisplayHub(PresentationBroadcaster broadcaster) : Hub
{
    /// <summary>The client method both hubs push snapshots to.</summary>
    public const string SnapshotMethod = "snapshot";

    /// <summary>
    /// Sends the current picture immediately, so a page that connects between two changes is not
    /// blank until the next one.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(SnapshotMethod, broadcaster.Latest);
        await base.OnConnectedAsync();
    }
}
