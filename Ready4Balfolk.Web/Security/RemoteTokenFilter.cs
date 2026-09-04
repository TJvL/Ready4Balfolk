using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Ready4Balfolk.Web.Hubs;

namespace Ready4Balfolk.Web.Security;

/// <summary>Checks on every command that the phone sending it is still let in.</summary>
/// <remarks>
/// <para>
/// A socket is checked when it opens and then never again, so a phone that was connected when the
/// PIN changed kept a working remote: the whole point of generating a new PIN is that the helper
/// who had the old one is turned out, and that has to hold for the connection they already have.
/// </para>
/// <para>
/// The token is told it is finished before the connection goes, because a remote that quietly stops
/// working is indistinguishable from an application that has crashed, and somebody at the bar will
/// stand there tapping.
/// </para>
/// </remarks>
public sealed class RemoteTokenFilter(RemoteAccessService access) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        if (access.IsTokenValid(TokenOf(invocationContext.Context)))
        {
            return await next(invocationContext).ConfigureAwait(false);
        }

        await invocationContext.Hub.Clients.Caller
            .SendAsync(RemoteHub.TurnedOutMethod).ConfigureAwait(false);

        // Thrown rather than answered, because there is no answer: every command here reaches the
        // evening, and this connection is not allowed to. The page has been told why.
        throw new HubException("This remote is no longer let in");
    }

    /// <summary>The token the connection was opened with, which is where SignalR keeps it.</summary>
    internal static string? TokenOf(HubCallerContext context) =>
        context?.Features.Get<IHttpContextFeature>()?.HttpContext?.Request.Query["access_token"];
}
