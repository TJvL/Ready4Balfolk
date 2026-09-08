using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
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
/// <para>
/// This is an extra the user switches on, not a backend the app talks to. Nothing in the desktop app
/// depends on it running, and it owns no state of its own beyond the listener.
/// </para>
/// <para>
/// The last constructor parameter is how this machine's networks are read. It defaults to reading
/// them off the operating system and is handed in by tests: a laptop on a container bridge and
/// nothing else, or a laptop on no network at all, are the cases the QR code gets wrong, and
/// neither can be arranged on a build agent.
/// </para>
/// </remarks>
public sealed class PresentationWebServer(
    IServiceProvider hostServices,
    ILoggerService logger,
    TimeProvider time,
    Func<IReadOnlyList<NetworkAdapter>>? adapters = null)
    : IAsyncDisposable
{
    private readonly Func<IReadOnlyList<NetworkAdapter>> _adapters = adapters ?? LocalAddresses.ThisMachine;
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

    /// <summary>
    /// Addresses another device stands a chance of reaching, best guess first, for the settings
    /// panel to print and the QR code to carry.
    /// </summary>
    /// <remarks>
    /// Empty while the server is down, and empty on a machine that is on no network at all: there
    /// is nothing to hand a phone then, and an address only this machine can use is worse than
    /// none. Worked out on every read rather than kept from the start, because a cable plugged in
    /// or a wifi joined halfway through an evening changes the answer and nothing restarts the
    /// listener.
    /// </remarks>
    public IReadOnlyList<string> Addresses =>
        _running is { } options ? LocalAddresses.Reachable(_adapters(), options.Port) : [];

    /// <summary>The port the listener actually bound, or null while there is no listener.</summary>
    /// <remarks>
    /// Not the port in the settings, which is what the spinner shows and can already have been
    /// changed to one nothing is listening on. The settings panel prints this when the machine is
    /// on no network another device can reach, so that the pages can still be opened here.
    /// </remarks>
    public int? BoundPort => _running?.Port;

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
        WebApplication? app = null;

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

            builder.Services.AddSingleton<RemoteTokenFilter>();
            builder.Services
                .AddSignalR()
                // Every command from a phone, not only the socket it arrives on: a PIN change has
                // to turn out the connection the helper already has.
                .AddHubOptions<RemoteHub>(options => options.AddFilter<RemoteTokenFilter>());
            builder.Services.AddForwardedHostServices(hostServices);
            builder.Services.AddSingleton(_access);
            builder.Services.AddSingleton<PresentationBroadcaster>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<PresentationBroadcaster>());

            app = builder.Build();

            var assets = new ManifestEmbeddedFileProvider(typeof(PresentationWebServer).Assembly, "wwwroot");
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = assets,
                // Heuristic caching otherwise lets a phone keep serving a script from before the
                // last upgrade for days: no freshness directive plus a Last-Modified is exactly
                // what makes a browser guess an expiry instead of asking. The ETag this already
                // sends still makes a repeat visit a cheap 304, so this is not "reload every file
                // every time", only "always ask first".
                OnPrepareResponse = context =>
                    context.Context.Response.Headers.CacheControl = "no-cache",
            });

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

            // The port and how many, never the addresses themselves. Which interfaces this
            // machine has is the DJ's home or the venue's network, and the settings panel is where
            // somebody who needs an address reads one. That none is reachable is worth saying,
            // because it is why the QR code is not on offer.
            var addresses = Addresses;
            await logger.InfoAsync(addresses.Count > 0
                    ? $"Presentation server listening on port {options.Port} ({addresses.Count} addresses)"
                    : $"Presentation server listening on port {options.Port}, with no address another device can reach")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Almost always the port already being in use. The app carries on without the server,
            // and the settings panel shows the failure rather than a switch that claims success.
            LastError = ex.Message;
            await logger.ErrorAsync($"Presentation server could not start on port {options.Port}", ex)
                .ConfigureAwait(false);
            await DisposeUnstartedAsync(app).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            await logger.ErrorAsync("Presentation server failed to start", ex).ConfigureAwait(false);
            await DisposeUnstartedAsync(app).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes a host built by a start attempt that did not make it into <see cref="_app"/>.
    /// </summary>
    /// <remarks>
    /// A reference check rather than a bool: everything between <c>StartAsync</c> succeeding and
    /// the end of the try block is bookkeeping around a host that is genuinely up, and if one of
    /// those steps is what threw, the host must be left running, not torn down under it.
    /// </remarks>
    private async Task DisposeUnstartedAsync(WebApplication? app)
    {
        if (app is not null && !ReferenceEquals(_app, app))
        {
            await app.DisposeAsync().ConfigureAwait(false);
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
