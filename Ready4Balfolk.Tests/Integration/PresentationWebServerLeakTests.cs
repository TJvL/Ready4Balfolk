using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Integration;

/// <summary>The test classes that measure something only the whole process can count.</summary>
/// <remarks>
/// Open file descriptors belong to the process, not to a test, and test classes run in parallel
/// by default: whatever another class opened or closed while one of these was measuring was put
/// down to the server under test. On a four-core machine the full suite moved the count by more
/// than the leak being looked for with nothing leaking at all. Run alone, after everything else
/// has finished, the only thing opening handles during the measurement is the test itself.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessWideHandleCount
{
    public const string Name = "Process-wide handle count";
}

/// <summary>
/// A host that is never disposed holds on to everything it built: its own DI container, SignalR
/// hubs and hosted services. Two ways out of the server used to throw one away like that. A failed
/// start did, every time the DJ toggled the server while something else already held the port, and
/// so did a stop that threw or ran out of time, because the disposal sat after the stop rather than
/// behind it.
/// </summary>
[Collection(ProcessWideHandleCount.Name)]
public sealed class PresentationWebServerLeakTests
{
    /// <summary>
    /// Counted in open file descriptors, where an undisposed host costs exactly one apiece: twenty
    /// failed starts move this from zero to twenty, which is the whole signal. Windows counts
    /// handles on its own terms and drifts by about that much across the same twenty attempts with
    /// nothing leaking at all, so the measurement is worthless there even though the defect is not
    /// platform specific.
    /// </summary>
    [Fact]
    public async Task ARepeatedFailedStart_DoesNotLeakTheHostItBuilt()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux(),
            "Handle counting only separates a leaked host from ordinary drift on Linux.");

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

    /// <summary>
    /// A stop that does not finish cleanly is a client that would not let go in time, or a hosted
    /// service that threw on the way down. The listener is gone either way, but the host that owned
    /// it still has to be disposed, and the same one descriptor apiece shows whether it was.
    /// </summary>
    [Fact]
    public async Task ARepeatedStopThatRunsOutOfTime_DoesNotLeakTheHostItStopped()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux(),
            "Handle counting only separates a leaked host from ordinary drift on Linux.");

        using var hostServices = RunningWebServer.HostServices();
        var log = new RecordingLoggerService();

        // The broadcaster lets go of the presentation when the host stops it, so this is where the
        // stop is made to run out of time. Counted, because a stop that never reached it would
        // leave nothing to dispose late and make the assertion below pass for the wrong reason.
        var stopsThatRanOut = 0;
        hostServices.GetRequiredService<IPresentationStateService>().WhenStateChanged.Returns(
            Observable.Create<PresentationState>(_ => Disposable.Create(() =>
            {
                Interlocked.Increment(ref stopsThatRanOut);
                throw new OperationCanceledException("The drain ran out of time.");
            })));

        await using var server =
            new PresentationWebServer(hostServices, log, TimeProvider.System, () => []);

        await StartAndFailToStopRepeatedlyAsync(server, times: 5);
        var baseline = SettledHandleCount();

        await StartAndFailToStopRepeatedlyAsync(server, times: 20);

        var afterTwentyMoreStops = SettledHandleCount();

        Assert.Equal(25, Volatile.Read(ref stopsThatRanOut));
        Assert.True(
            afterTwentyMoreStops - baseline < 15,
            $"Handle count grew from {baseline} to {afterTwentyMoreStops} over twenty more stops " +
            "that ran out of time, which is what an undisposed host per stop looks like.");
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

    /// <summary>Port zero, so every start binds a port nothing else in the run can be holding.</summary>
    private static async Task StartAndFailToStopRepeatedlyAsync(PresentationWebServer server, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await server.ApplyAsync(
                new WebServerOptions(true, 0, false, ""), TestContext.Current.CancellationToken);
            Assert.Equal(WebServerState.Running, server.State);

            await server.ApplyAsync(
                new WebServerOptions(false, 0, false, ""), TestContext.Current.CancellationToken);
            Assert.Equal(WebServerState.Stopped, server.State);
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
