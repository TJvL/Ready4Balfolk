using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Web.Hubs;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Web;

/// <summary>The embedded server, started and stopped by the app that hosts it.</summary>
/// <remarks>
/// This is an extra the user switches on, not a backend the app talks to. Nothing in the desktop app
/// depends on it running, and it owns no state of its own beyond the listener.
/// </remarks>
public sealed class PresentationWebServer(
    IServiceProvider hostServices, ILoggerService logger, TimeProvider time)
    : IAsyncDisposable
{
    private readonly RemoteAccessService _access = new(time);
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Subject<Unit> _changed = new();

    private WebApplication? _app;
    private WebServerOptions? _running;
    private WebServerOptions? _desired;

    /// <summary>
    /// Fires after every start or stop, so the settings panel can show what actually happened
    /// rather than what the switch was set to.
    /// </summary>
    public IObservable<Unit> WhenChanged => _changed.AsObservable();

    /// <summary>Whether the listener is up.</summary>
    public bool IsRunning => _app is not null;

    /// <summary>What the server is doing, including the slow bits in between.</summary>
    public WebServerState State { get; private set; } = WebServerState.Stopped;

    /// <summary>Why the last start attempt failed, or null when it did not.</summary>
    public string? LastError { get; private set; }

    /// <summary>Addresses a browser can reach, for the settings panel to print.</summary>
    public IReadOnlyList<string> Addresses { get; private set; } = [];

    /// <summary>Brings the server into line with the settings, starting or stopping as needed.</summary>
    public async Task ApplyAsync(WebServerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Latest wins. Flicking the switch back and forth otherwise queues a full bind and drain
        // for every click, and the socket keeps churning long after the user has stopped.
        _desired = options;

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_desired != options)
            {
                return;
            }

            // The PIN and the remote switch are read live, so they never need a restart.
            _access.Configure(options.RemoteControlEnabled, options.RemoteControlPin);

            if (!options.Enabled)
            {
                if (_app is null)
                {
                    SetState(WebServerState.Stopped);
                    return;
                }

                SetState(WebServerState.Stopping);
                await StopCoreAsync().ConfigureAwait(false);
                SetState(WebServerState.Stopped);
                return;
            }

            if (_app is not null && _running is not null && !_running.RequiresRestart(options))
            {
                _running = options;
                SetState(WebServerState.Running);
                return;
            }

            if (_app is not null)
            {
                SetState(WebServerState.Stopping);
                await StopCoreAsync().ConfigureAwait(false);
            }

            SetState(WebServerState.Starting);
            await StartCoreAsync(options, cancellationToken).ConfigureAwait(false);
            SetState(_app is not null ? WebServerState.Running : WebServerState.Failed);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private void SetState(WebServerState state)
    {
        State = state;
        _changed.OnNext(Unit.Default);
    }

    private async Task StartCoreAsync(WebServerOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var builder = WebApplication.CreateSlimBuilder();

            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(new AppLogBridgeProvider(logger));

            builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Any, options.Port));

            // Kestrel's graceful shutdown waits for connections to drain, and a display page left
            // open in a hall holds a WebSocket indefinitely. The default 30 seconds would be spent
            // with the app refusing to quit, so give it two and take the socket back.
            builder.Services.Configure<HostOptions>(host =>
                host.ShutdownTimeout = TimeSpan.FromSeconds(2));

            builder.Services.AddSignalR();
            builder.Services.AddForwardedHostServices(hostServices);
            builder.Services.AddSingleton(_access);
            builder.Services.AddSingleton<PresentationBroadcaster>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<PresentationBroadcaster>());

            var app = builder.Build();

            var assets = new ManifestEmbeddedFileProvider(typeof(PresentationWebServer).Assembly, "wwwroot");
            app.UseStaticFiles(new StaticFileOptions { FileProvider = assets });

            app.MapGet("/", () => ServeAsset(assets, "display.html"));

            app.MapGet("/remote", () => _access.IsEnabled
                ? ServeAsset(assets, "remote.html")
                : Results.NotFound());

            // The pages localize themselves, but the language is the app's setting rather than the
            // browser's: a Dutch projector next to a Dutch desktop window must not read English.
            app.MapGet("/api/config", (ISettingsStore settings) => Results.Ok(new WebConfigDto(
                settings.Current.ApplicationLanguage == ApplicationLanguage.Dutch ? "nl" : "en",
                _access.IsEnabled)));

            app.MapPost("/api/remote/login", (RemoteLoginRequest request, HttpContext http) =>
            {
                var clientKey = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = _access.TryLogin(request.Pin, clientKey);
                return result.IsGranted
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: StatusCodes.Status401Unauthorized);
            });

            app.MapHub<DisplayHub>("/hubs/display");
            app.MapHub<RemoteHub>("/hubs/remote");

            await app.StartAsync(cancellationToken).ConfigureAwait(false);

            _app = app;
            _running = options;
            LastError = null;
            Addresses = DescribeAddresses(options);

            await logger.InfoAsync($"Presentation server listening on {string.Join(", ", Addresses)}")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Almost always the port already being in use. The app carries on without the server,
            // and the settings panel shows the failure rather than a switch that claims success.
            LastError = ex.Message;
            Addresses = [];
            await logger.ErrorAsync($"Presentation server could not start on port {options.Port}", ex)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Addresses = [];
            await logger.ErrorAsync("Presentation server failed to start", ex).ConfigureAwait(false);
        }
    }

    private async Task StopCoreAsync()
    {
        if (_app is null)
        {
            return;
        }

        var app = _app;
        _app = null;
        _running = null;
        Addresses = [];

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await app.StopAsync(deadline.Token).ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
            await logger.InfoAsync("Presentation server stopped").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A client refused to let go. The listener is gone either way, and the process is
            // either quitting or about to rebind, so there is nothing to recover.
            await logger.WarningAsync("Presentation server did not drain in time").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await logger.ErrorAsync("Presentation server failed to stop cleanly", ex).ConfigureAwait(false);
        }
    }

    private static IResult ServeAsset(ManifestEmbeddedFileProvider assets, string name)
    {
        var file = assets.GetFileInfo(name);
        return file.Exists
            ? Results.Stream(file.CreateReadStream(), "text/html; charset=utf-8")
            : Results.NotFound();
    }

    /// <summary>
    /// What to type into the other device: every address this machine actually answers on. Guessing
    /// which interface the hall's wifi handed out is not something to make the user do.
    /// </summary>
    private static List<string> DescribeAddresses(WebServerOptions options)
    {
        var addresses = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    addresses.Add($"http://{address.Address}:{options.Port}");
                }
            }
        }

        if (addresses.Count == 0)
        {
            addresses.Add($"http://localhost:{options.Port}");
        }

        return addresses;
    }

    public async ValueTask DisposeAsync()
    {
        _desired = null;
        await StopCoreAsync().ConfigureAwait(false);
        State = WebServerState.Stopped;
        _changed.Dispose();
        _mutex.Dispose();
    }
}

/// <summary>The body of a PIN exchange.</summary>
public sealed record RemoteLoginRequest(string? Pin);

/// <summary>What a page needs to know before it draws anything.</summary>
public sealed record WebConfigDto(string Language, bool RemoteEnabled);
