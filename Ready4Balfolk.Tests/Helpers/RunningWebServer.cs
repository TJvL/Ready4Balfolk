using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Presentation;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.Helpers;

/// <summary>A presentation server that really bound a port, with the machine's networks handed in.</summary>
/// <remarks>
/// The address in the QR code is only wrong while the server is up, and nothing reports Running
/// until a listener actually bound, so these tests bind one on a free port. What the machine is on
/// is supplied rather than read: a laptop on a container bridge and the hall's wifi, and a laptop
/// on no network at all, are the two cases that go wrong and neither can be arranged on a build
/// agent.
/// </remarks>
internal sealed class RunningWebServer : IAsyncDisposable
{
    private readonly ServiceProvider _hostServices;

    private RunningWebServer(ServiceProvider hostServices, PresentationWebServer server, int port)
    {
        _hostServices = hostServices;
        Server = server;
        Port = port;
    }

    public PresentationWebServer Server { get; }

    /// <summary>The port it bound, which is the one every address it offers carries.</summary>
    public int Port { get; }

    public static Task<RunningWebServer> StartAsync(params NetworkAdapter[] adapters) =>
        StartAsync(() => adapters);

    /// <summary>The same with the networks read afresh on every ask, as the real one reads them.</summary>
    public static async Task<RunningWebServer> StartAsync(Func<IReadOnlyList<NetworkAdapter>> adapters)
    {
        var hostServices = HostServices();
        var port = FreePort();
        var log = new RecordingLoggerService();
        var server = new PresentationWebServer(hostServices, log, TimeProvider.System, adapters);

        await server.ApplyAsync(new WebServerOptions(true, port, false, ""));

        // A server that failed to bind reports Failed and offers no addresses, which would make
        // every assertion below pass for the wrong reason.
        Assert.True(
            server.State == WebServerState.Running,
            $"The test server did not start: {string.Join("\n", log.Errors.Select(e => e.Exception?.ToString() ?? e.Message))}");

        return new RunningWebServer(hostServices, server, port);
    }

    public async ValueTask DisposeAsync()
    {
        await Server.DisposeAsync();
        await _hostServices.DisposeAsync();
    }

    /// <summary>The app's own singletons, which the web host is handed rather than building.</summary>
    /// <remarks>
    /// The broadcaster is a hosted service, so it starts with the listener and immediately draws
    /// the current picture. A bare substitute hands it a null state and the start fails, which
    /// would look here like a server that could not bind.
    /// </remarks>
    /// <remarks>
    /// Internal rather than private: a test that never gets past a failed start still needs a
    /// real set of host services to build the server against.
    /// </remarks>
    internal static ServiceProvider HostServices()
    {
        var presentation = Substitute.For<IPresentationStateService>();
        presentation.Current.Returns(new PresentationState(
            PresentationItem.None, PresentationItem.None, PresentationItem.None, IsPlaying: false));
        presentation.WhenStateChanged.Returns(Observable.Never<PresentationState>());
        presentation.WhenProgressChanged.Returns(Observable.Never<PresentationProgress>());

        var queue = Substitute.For<IQueueService>();
        queue.Connect().Returns(new SourceList<IQueueItem>().Connect());
        queue.Items.Returns([]);

        var services = new ServiceCollection();
        services.AddSingleton(presentation);
        services.AddSingleton(queue);
        services.AddSingleton(Substitute.For<IQueueConsumptionService>());
        services.AddSingleton(Substitute.For<IEndOfNightAudio>());
        services.AddSingleton(Substitute.For<IRandomTrackService>());
        services.AddSingleton(Substitute.For<IDancePool>());
        services.AddSingleton(Substitute.For<ITrackStore>());
        services.AddSingleton(Substitute.For<ISettingsStore>());
        services.AddSingleton<ILoggerService>(new NoOpLoggerService());
        services.AddSingleton(Substitute.For<IRemoteCommandDispatcher>());
        return services.BuildServiceProvider();
    }

    /// <summary>A port the operating system says is free, rather than one picked out of the air.</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
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
