using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Ready4Balfolk.Web.Hubs;

namespace Ready4Balfolk.Web.Security;

/// <summary>Every phone that is holding a remote socket, so a PIN change can close them.</summary>
/// <remarks>
/// <para>
/// The token is checked on the way in and on every command, which stops a turned-out phone doing
/// anything but not from watching: the broadcaster pushes the queue and the current dance to
/// everything connected, so a helper who simply sits there keeps the evening on their screen until
/// they press something. The DJ changed the PIN to get that helper out.
/// </para>
/// <para>
/// Only the remote's sockets are in here. The display page needs no PIN and never did, because it
/// is the projector in the corner of the hall and anybody on the network is meant to be able to
/// open it. Nothing about changing a PIN may put that screen out.
/// </para>
/// </remarks>
public sealed class RemoteConnections(IHubContext<RemoteHub> remoteHub, RemoteAccessService access)
{
    /// <summary>How long a phone gets to be told before its socket goes anyway.</summary>
    /// <remarks>
    /// The notice travels over the very socket that is about to be closed, and a phone that has
    /// been carried out of range of the hall's wifi will never read it. Waiting on that phone is
    /// the settings screen hanging while the DJ stands at the desk, so closing is the guarantee and
    /// telling is the courtesy.
    /// </remarks>
    private static readonly TimeSpan NoticeDeadline = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new(StringComparer.Ordinal);

    /// <summary>Remembers a socket that got past the token check, unless it has just lost it.</summary>
    /// <returns>Whether the socket may carry on. A false has already been told and closed.</returns>
    /// <remarks>
    /// The token is read again here, after the socket is in the register, because of the gap
    /// between the hub checking it and this line. A PIN change landing in that gap drops the
    /// tokens and then sweeps a register this socket is not in yet, and the phone would be left
    /// connected on a token nothing will honour again: exactly the phone the DJ changed the PIN
    /// to get rid of. Registering first and reading the token second closes the gap from the other
    /// end, since the drop happens before the sweep: either the sweep finds this socket, or this
    /// read finds the dropped token.
    /// </remarks>
    public async Task<bool> AddAsync(HubCallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _connections[context.ConnectionId] = context;

        if (access.IsTokenValid(RemoteTokenFilter.TokenOf(context)))
        {
            return true;
        }

        await TurnOutAsync(context.ConnectionId, context).ConfigureAwait(false);
        return false;
    }

    /// <summary>Forgets a socket that has gone.</summary>
    public void Remove(HubCallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _connections.TryRemove(context.ConnectionId, out _);
    }

    /// <summary>Closes every remote whose token has stopped being good, telling it why first.</summary>
    /// <remarks>
    /// Told first, because a socket that simply dies is the venue's wifi as far as the phone can
    /// tell, and the page will spend the rest of the evening trying to reconnect. The same message
    /// the filter sends puts the PIN form back with a line saying why it is there.
    /// <para>
    /// Whether a connection is stale is read off its own token rather than assumed from the caller
    /// having just changed the PIN, so this asks the same question of the same token as the check
    /// on the way in and the check on every command. That is what lets a socket registered a
    /// moment ago be closed down this same path, and it is why nothing here has to know why it was
    /// called.
    /// </para>
    /// </remarks>
    public Task TurnOutStaleAsync() => Task.WhenAll(_connections
        .Where(connection => !access.IsTokenValid(RemoteTokenFilter.TokenOf(connection.Value)))
        .Select(connection => TurnOutAsync(connection.Key, connection.Value)));

    private async Task TurnOutAsync(string connectionId, HubCallerContext context)
    {
        _connections.TryRemove(connectionId, out _);

        using var notice = new CancellationTokenSource(NoticeDeadline);

        try
        {
            await remoteHub.Clients.Client(connectionId)
                .SendAsync(RemoteHub.TurnedOutMethod, notice.Token)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Whatever the notice ran into, it ran into it on a socket that is being closed
            // anyway: a phone carried out of the hall, or a host being torn down underneath. The
            // connection has already left the register, so an abort skipped here is a phone
            // nothing will ever close and no later PIN change will find to try again. That is
            // also why this does not rethrow: the only caller in the app is a settings save
            // running unwatched, and one phone's send must not take the rest of the sweep with it.
        }
        finally
        {
            context.Abort();
        }
    }
}
