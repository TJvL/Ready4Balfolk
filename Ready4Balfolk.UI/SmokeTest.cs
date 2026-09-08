using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.UI;

/// <summary>
/// The <c>--smoke-test</c> entry point: starts the application for real, checks that everything a
/// package can plausibly ship broken actually came up, and exits with a status code instead of
/// waiting for a user.
/// </summary>
/// <remarks>
/// CI packages every artifact but cannot judge one by looking at it. The failure this exists to
/// catch is a native library that is missing from the package or fails to load out of it, which
/// looks identical to a healthy build until someone runs it. Killing the app after a timeout would
/// not catch that: <see cref="IAudioPlaybackService"/> is a lazy singleton that nothing on the
/// startup path resolves, so a build with no BASS at all reaches a running window quite happily.
/// </remarks>
internal static class SmokeTest
{
    private const int Passed = 0;
    private const int Failed = 1;
    private const int HungOrCrashed = 2;

    /// <summary>
    /// The port the presentation server is asked for. Deliberately not the port the application
    /// defaults to, so a smoke test run on a machine where Ready4Balfolk is already serving its
    /// display page does not fail over a port that is legitimately taken.
    /// </summary>
    private const int WebServerPort = 18420;

    /// <summary>
    /// What a browser pulls in after the display page, and the only files that reach it through
    /// the static file middleware. The page itself has a route of its own, so fetching it proves
    /// nothing about these.
    /// </summary>
    private static readonly string[] ServedAssets = ["display.js", "app.css", "strings.js", "remote.js"];

    /// <summary>Cold start on a runner with software rendering is slow, but not this slow.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// What the checks after the window get to themselves. Separate from the startup budget
    /// because they are slow in their own right, and because an overrun in a decode or in the
    /// presentation server reported as a window that never opened sends whoever reads it to
    /// Avalonia startup, which is the one place the fault is not.
    /// </summary>
    private static readonly TimeSpan ChecksTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The stores load asynchronously off the window's Opened event and nothing on the UI thread
    /// awaits them, so give them a moment to fail before the log is judged.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);

    /// <summary>How long a graceful shutdown gets before the process is ended from under it.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Every extension the application offers to open. Two of them have no fixture in
    /// scripts/smoke-test-media: .aif is the same decoder as .aiff, and nothing encodes MPEG audio
    /// layer 1 any more, so those two are covered by this list alone and not by a decode.
    /// </summary>
    private static readonly string[] ExpectedExtensions =
        [".mp1", ".mp2", ".mp3", ".wav", ".aif", ".aiff", ".ogg", ".flac"];

    /// <summary>What every file in scripts/smoke-test-media lasts, and how far off it may decode.</summary>
    private static readonly TimeSpan MediaDuration = TimeSpan.FromSeconds(1.5);

    /// <summary>Generous, because the lossy encoders pad the stream with an encoder delay.</summary>
    private static readonly TimeSpan MediaDurationTolerance = TimeSpan.FromSeconds(0.5);

    /// <summary>Long enough for a loopback request, short enough to fail the check not the run.</summary>
    private static readonly TimeSpan WebRequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Static so the watchdog survives: the only stack that could root it is the one blocked in
    /// the dispatcher main loop, and a collected timer never fires.
    /// </summary>
    private static Timer? _watchdog;

    public static int Run(AppBuilder builder, string[] args)
    {
        var mediaDirectory = ReadOption(args, "--smoke-test-media");

        var logFile = new FileInfo(Path.Combine(
            new ApplicationSettingsDirectory(new FileSystem()).DirectoryInfoRoot.FullName, "app.log"));

        // Judge only what this run wrote. A developer's existing log is left alone, which also
        // means a second run in the same workspace does not inherit the first one's verdict.
        logFile.Refresh();
        var logOffset = logFile.Exists ? logFile.Length : 0L;

        _watchdog = new Timer(
            _ =>
            {
                Report($"timed out after {StartupTimeout.TotalSeconds:0} s without reaching a running window");
                DumpLog(logFile, logOffset);
                // A startup that hangs never gives the UI thread back, so there is nothing to
                // unwind: end the process from the timer thread.
                Environment.Exit(HungOrCrashed);
            },
            null,
            StartupTimeout,
            Timeout.InfiniteTimeSpan);

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args,
            // Nothing closes this window but the check below, and App skips its exit confirmation
            // in smoke test mode, so the exit code is decided in exactly one place.
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        builder.SetupWithLifetime(lifetime);

        var mainWindow = lifetime.MainWindow;
        if (mainWindow is null)
        {
            Report("the desktop lifetime produced no main window");
            return Failed;
        }

        // FinishAsync handles its own failures and never throws, so there is nothing here to await.
        mainWindow.Opened += (_, _) => DispatcherTimer.RunOnce(
            () => _ = FinishAsync(lifetime, logFile, logOffset, mediaDirectory), SettleDelay);

        return lifetime.Start(args);
    }

    private static async Task FinishAsync(
        ClassicDesktopStyleApplicationLifetime lifetime,
        FileInfo logFile,
        long logOffset,
        string? mediaDirectory)
    {
        // The window opened, which is the question the startup watchdog was asked. Hand the rest
        // of the run a budget of its own so an overrun here is reported as what it is.
        _watchdog?.Dispose();
        _watchdog = new Timer(
            _ =>
            {
                Report($"the checks did not finish within {ChecksTimeout.TotalSeconds:0} s");
                DumpLog(logFile, logOffset);
                Environment.Exit(HungOrCrashed);
            },
            null,
            ChecksTimeout,
            Timeout.InfiniteTimeSpan);

        var failures = new List<string>();
        var decoded = 0;

        try
        {
            // This resolve is the whole point: it is the first and only thing that loads libbass.
            var audio = App.Services.GetRequiredService<IAudioPlaybackService>();

            // No point decoding anything if BASS never came up; it would only repeat the news.
            if (CheckAudio(audio, failures))
            {
                if (mediaDirectory is null)
                {
                    Report("no --smoke-test-media directory given, so no format was actually decoded");
                }
                else
                {
                    decoded = await CheckMediaAsync(audio, mediaDirectory, failures);
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add($"the audio checks threw: {ex}");
        }

        try
        {
            await CheckWebServerAsync(failures);
        }
        catch (Exception ex)
        {
            failures.Add($"the presentation server check threw: {ex}");
        }

        failures.AddRange(ReadLoggedFailures(logFile, logOffset));

        int exitCode;
        if (failures.Count == 0)
        {
            Report($"passed: window opened, BASS, BASSFLAC and BASS_FX all loaded, "
                   + $"{decoded} media files decoded, display page and web assets served, log clean");
            exitCode = Passed;
        }
        else
        {
            foreach (var failure in failures)
            {
                Report($"FAILED: {failure}");
            }

            DumpLog(logFile, logOffset);
            exitCode = Failed;
        }

        // Shutdown runs the app's own teardown, which is worth exercising but is also the part
        // most able to hang on a background task. Swap the checks watchdog for one that carries
        // the verdict out regardless, so a stuck teardown cannot turn a decided run into a job
        // timeout, or worse, report a hang for a run that has already passed.
        _watchdog?.Dispose();
        _watchdog = new Timer(
            _ =>
            {
                Report($"shutdown did not complete within {ShutdownTimeout.TotalSeconds:0} s");
                Environment.Exit(exitCode);
            },
            null,
            ShutdownTimeout,
            Timeout.InfiniteTimeSpan);

        lifetime.Shutdown(exitCode);
    }

    /// <returns>Whether BASS came up, and so whether decoding anything is worth attempting.</returns>
    private static bool CheckAudio(IAudioPlaybackService audio, List<string> failures)
    {
        // WhenAvailabilityChanged replays its current value to a new subscriber, so subscribing
        // and immediately unsubscribing is how the present state is read.
        var isAvailable = false;
        audio.WhenAvailabilityChanged.Subscribe(value => isAvailable = value).Dispose();

        if (!isAvailable)
        {
            failures.Add("BASS did not initialise; the native library is missing or would not load");
        }

        if (!audio.IsEqualizerAvailable)
        {
            failures.Add("BASS_FX did not load; the equalizer would be unavailable to users");
        }

        // What the catalogue filters on, so an extension missing here is a format that silently
        // stops appearing in the app even though BASS could have played it.
        var missing = ExpectedExtensions
            .Where(extension => !SupportedAudioFormats.Extensions.Contains(extension))
            .ToList();

        if (missing.Count > 0)
        {
            failures.Add($"missing from the supported extensions: {string.Join(", ", missing)}");
        }

        return isAvailable;
    }

    /// <summary>
    /// Opens every fixture in <paramref name="mediaDirectory"/> and checks it decodes to the length
    /// it should be. Registering a plugin is not the same as being able to read a file with it:
    /// the Windows builds shipped for a release with BASSFLAC present and unloadable, and a
    /// duration that comes back right is the cheapest proof that real samples were read.
    /// </summary>
    /// <returns>How many files decoded.</returns>
    private static async Task<int> CheckMediaAsync(
        IAudioPlaybackService audio,
        string mediaDirectory,
        List<string> failures)
    {
        if (!Directory.Exists(mediaDirectory))
        {
            failures.Add($"no media directory at {mediaDirectory}");
            return 0;
        }

        var files = Directory.GetFiles(mediaDirectory).OrderBy(file => file, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            failures.Add($"no media files in {mediaDirectory}");
            return 0;
        }

        var decoded = 0;

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);

            if (!SupportedAudioFormats.IsSupported(file))
            {
                failures.Add($"{name} is not a format this build offers to open");
                continue;
            }

            // WhenDurationChanged is a plain subject with no replay, so the subscription has to be
            // in place before the decode that raises it.
            var duration = TimeSpan.Zero;
            using (audio.WhenDurationChanged.Subscribe(value => duration = value))
            {
                try
                {
                    await audio.SelectAsync(new Uri(file));
                }
                catch (Exception ex)
                {
                    failures.Add($"{name} would not decode: {ex.Message}");
                    continue;
                }
            }

            if ((duration - MediaDuration).Duration() > MediaDurationTolerance)
            {
                failures.Add(
                    $"{name} decoded to {duration.TotalSeconds:0.###} s, "
                    + $"expected about {MediaDuration.TotalSeconds:0.###} s");
                continue;
            }

            decoded++;
        }

        await audio.ClearAsync();
        return decoded;
    }

    /// <summary>
    /// Starts the presentation server and fetches the display page, and every asset the page
    /// pulls in, from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two packaging failures hide behind a green build. The display page and its scripts are
    /// embedded in Ready4Balfolk.Web and served from the assembly rather than from disk, so a
    /// package that drops them starts perfectly and serves nothing; and a Flatpak whose manifest
    /// lost <c>--share=network</c> gets a sandbox with no network of its own, where the listener
    /// still binds inside the sandbox but no address the hall could reach exists at all.
    /// </para>
    /// <para>
    /// The switch is not touched: the server is driven straight through
    /// <see cref="PresentationWebServer.ApplyAsync"/>, so a developer running this locally does not
    /// find the presentation server left on in their settings afterwards.
    /// </para>
    /// </remarks>
    private static async Task CheckWebServerAsync(List<string> failures)
    {
        var server = App.Services.GetRequiredService<PresentationWebServer>();
        var options = new WebServerOptions(true, WebServerPort, false, string.Empty);

        try
        {
            await server.ApplyAsync(options);

            if (server.State is not WebServerState.Running)
            {
                failures.Add($"the presentation server did not start on port {WebServerPort}: "
                             + (server.LastError ?? "no reason was recorded"));
                return;
            }

            using var client = new HttpClient { Timeout = WebRequestTimeout };

            try
            {
                using (var page = await client.GetAsync(new Uri($"http://127.0.0.1:{WebServerPort}/")))
                {
                    if (!page.IsSuccessStatusCode)
                    {
                        failures.Add($"the display page came back as {(int)page.StatusCode}");
                    }
                }

                // The page has a route of its own that reads display.html straight out of the
                // assembly, so serving it says nothing about the scripts and the stylesheet: those
                // go through the static file middleware, and they are what a projector browser
                // 404s on when a package embedded the markup and none of the rest.
                foreach (var asset in ServedAssets)
                {
                    using var response =
                        await client.GetAsync(new Uri($"http://127.0.0.1:{WebServerPort}/{asset}"));

                    if (!response.IsSuccessStatusCode)
                    {
                        failures.Add($"{asset} came back as {(int)response.StatusCode}, "
                                     + "so the web assets are missing from this package");
                        continue;
                    }

                    if ((await response.Content.ReadAsStringAsync()).Length == 0)
                    {
                        failures.Add($"{asset} was served empty");
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                failures.Add($"the presentation server could not be read from: {ex.Message}");
            }

            // A listener nothing outside this machine can dial is a server that is up for nobody.
            // On a Flatpak with no network share this is empty, because the sandbox has only its
            // own loopback; a machine genuinely on no network reads the same, and saying so is
            // right either way.
            if (server.Addresses.Count == 0)
            {
                failures.Add("the presentation server is listening, but on no address another "
                             + "device could reach");
            }
        }
        finally
        {
            await server.ApplyAsync(options with { Enabled = false });
        }
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static List<string> ReadLoggedFailures(FileInfo logFile, long logOffset)
    {
        var text = ReadLogTail(logFile, logOffset);

        return text is null
            ? ["no log file was written, so the application never got as far as configuring logging"]
            : [.. text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("[ERROR]", StringComparison.Ordinal)
                               || line.Contains("[CRITICAL]", StringComparison.Ordinal))
                .Select(line => $"logged: {line}")];
    }

    /// <summary>
    /// The log rather than stdout is where the app records what went wrong, and under Flatpak it
    /// sits inside the sandbox where the CI script cannot reach it. Printing it from in here keeps
    /// every caller identical.
    /// </summary>
    private static void DumpLog(FileInfo logFile, long logOffset)
    {
        var text = ReadLogTail(logFile, logOffset);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Report($"log from {logFile.FullName}:");
        Console.Error.WriteLine(text);
    }

    private static string? ReadLogTail(FileInfo logFile, long logOffset)
    {
        logFile.Refresh();
        if (!logFile.Exists)
        {
            return null;
        }

        // The logger caps the file by moving it aside and starting over, so one that shrank is a
        // fresh file whose every line belongs to this run.
        var start = logFile.Length < logOffset ? 0 : logOffset;

        try
        {
            using var stream = logFile.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            // Written in the logger's own format so the scan below counts an unreadable log as
            // the failure it is, rather than as an empty one.
            return $"[ERROR] the smoke test could not read the log file: {ex.Message}";
        }
    }

    private static void Report(string message) => Console.Error.WriteLine($"smoke-test: {message}");
}
