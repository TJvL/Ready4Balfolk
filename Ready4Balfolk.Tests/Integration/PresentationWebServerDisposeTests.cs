using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// The app disposes the server on its way out without waiting, and a settings change can have a
/// start in flight at that moment. Disposal used to go ahead beside it rather than after it, which
/// threw away the lock and the change notifications from under the start, and left the listener it
/// went on to open with nothing left to close it.
/// </summary>
public sealed class PresentationWebServerDisposeTests
{
    [Fact]
    public async Task DisposingDuringAStart_StopsTheServerThatStartFinishes()
    {
        using var hostServices = RunningWebServer.HostServices();
        var log = new RecordingLoggerService();
        var server = new PresentationWebServer(hostServices, log, TimeProvider.System, () => []);
        var port = FreePort();

        // Disposed from inside the notification that the start has begun, which is the one moment
        // that is certainly after the start took the lock and before it bound anything. Waiting
        // for a start that is merely slow enough would be a race the test usually wins.
        Task? disposing = null;
        using var trigger = server.WhenChanged
            .Where(_ => server.State == WebServerState.Starting)
            .Take(1)
            .Subscribe(_ => disposing = server.DisposeAsync().AsTask());

        await server.ApplyAsync(
            new WebServerOptions(true, port, false, ""), TestContext.Current.CancellationToken);

        Assert.NotNull(disposing);
        await disposing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(WebServerState.Stopped, server.State);

        // The listener is what a server left running gives away: the port is still taken.
        var rebound = new TcpListener(IPAddress.Any, port);
        rebound.Start();
        rebound.Stop();
    }

    [Fact]
    public async Task ApplyingAfterDisposal_DoesNothing()
    {
        // A settings change on its way in while the window closes lands after the server is gone.
        // It must neither throw at the app as it quits nor bring a listener back up.
        using var hostServices = RunningWebServer.HostServices();
        var log = new RecordingLoggerService();
        var server = new PresentationWebServer(hostServices, log, TimeProvider.System, () => []);
        var port = FreePort();

        await server.DisposeAsync();
        await server.ApplyAsync(
            new WebServerOptions(true, port, false, ""), TestContext.Current.CancellationToken);

        Assert.Equal(WebServerState.Stopped, server.State);
        Assert.Null(server.BoundPort);
    }

    /// <summary>A port the operating system says is free, rather than one picked out of the air.</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Any, 0);
        try
        {
            probe.Start();
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }
}
