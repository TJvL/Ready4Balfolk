namespace Ready4Balfolk.Web;

/// <summary>What the embedded server should currently be doing.</summary>
/// <param name="Enabled">Whether it should be listening at all.</param>
/// <param name="Port">The TCP port, already clamped by the settings record.</param>
/// <param name="RemoteControlEnabled">Whether the remote page and its hub exist at all.</param>
/// <param name="RemoteControlPin">The PIN the remote exchanges for a connection token.</param>
/// <remarks>
/// There is no bind-to-loopback option. A browser on this machine has nothing to offer that the
/// built-in presentation window does not already do better, so the only reason this server exists
/// is a device that is not this one: a laptop at the projector, or a phone in a pocket.
/// </remarks>
public sealed record WebServerOptions(
    bool Enabled,
    int Port,
    bool RemoteControlEnabled,
    string RemoteControlPin)
{
    /// <summary>Whether a change needs the listener torn down and rebuilt rather than just updated.</summary>
    /// <remarks>
    /// The PIN and the remote switch are read live by <c>RemoteAccessService</c>, so only the things
    /// baked into the Kestrel binding force a restart.
    /// </remarks>
    public bool RequiresRestart(WebServerOptions other) => other.Port != Port;
}
