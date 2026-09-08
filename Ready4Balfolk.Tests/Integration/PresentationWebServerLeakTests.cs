using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>
/// A failed start used to throw away the host it had already built, without disposing it. Every
/// attempt on a taken port built a full ASP.NET Core host, complete with its own DI container,
/// SignalR hubs and hosted services, and then leaked all of it. The DJ hits this by toggling the
/// server while something else already holds the port.
/// </summary>
public sealed class PresentationWebServerLeakTests
{
    /// <summary>
    /// <see cref="Process.HandleCount"/> is real handles on Windows, which is what CI runs on, and
    /// mirrors open file descriptors on Linux, so it is one signal that reads the same on both. A
    /// live host keeps a handful of handles open for its own housekeeping; twenty undisposed ones
    /// would not.
    /// </summary>
    [Fact]
    public async Task ARepeatedFailedStart_DoesNotLeakTheHostItBuilt()
    {
        using var hostServices = RunningWebServer.HostServices();
        var log = new RecordingLoggerService();

        // Something else holds the port for the whole test, the way another process on stage
        // already having it would. It has to hold the same wildcard address the server binds:
        // Windows lets a wildcard bind and a loopback bind of one port live side by side, so
        // blocking loopback alone leaves the server free to start.
        var blocker = new TcpListener(IPAddress.Any, 0);
        blocker.Start();
        var port = ((IPEndPoint)blocker.LocalEndpoint).Port;

        try
        {
            await using var server =
                new PresentationWebServer(hostServices, log, TimeProvider.System, () => []);

            // Warms up the JIT and the thread pool, whose one-off handles would otherwise be
            // mistaken below for a leak that the failed starts caused.
            await FailToStartRepeatedlyAsync(server, port, times: 5);
            var baseline = SettledHandleCount();

            await FailToStartRepeatedlyAsync(server, port, times: 20);

            var afterTwentyMoreFailures = SettledHandleCount();

            Assert.True(
                afterTwentyMoreFailures - baseline < 15,
                $"Handle count grew from {baseline} to {afterTwentyMoreFailures} over twenty " +
                "more failed starts, which is what an undisposed host per attempt looks like.");
        }
        finally
        {
            blocker.Stop();
        }
    }

    private static async Task FailToStartRepeatedlyAsync(
        PresentationWebServer server, int port, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await server.ApplyAsync(
                new WebServerOptions(true, port, false, ""), TestContext.Current.CancellationToken);
            Assert.Equal(WebServerState.Failed, server.State);
        }
    }

    /// <summary>Forces a full collection first so a handle only a finalizer would release is
    /// counted as gone, rather than as still pending.</summary>
    private static long SettledHandleCount()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        return process.HandleCount;
    }
}
