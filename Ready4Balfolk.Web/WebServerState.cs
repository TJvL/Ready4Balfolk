namespace Ready4Balfolk.Web;

/// <summary>What the embedded server is doing right now.</summary>
/// <remarks>
/// Binding and unbinding a socket, and letting Kestrel drain, takes long enough to see. A switch
/// that reports only its own position leaves the user clicking again to find out what happened, so
/// the transitions are states in their own right rather than gaps between them.
/// </remarks>
public enum WebServerState
{
    /// <summary>Not listening, and not asked to.</summary>
    Stopped,

    /// <summary>Binding the socket.</summary>
    Starting,

    /// <summary>Listening.</summary>
    Running,

    /// <summary>Releasing the socket.</summary>
    Stopping,

    /// <summary>The last start attempt failed, usually because the port is taken.</summary>
    Failed
}
