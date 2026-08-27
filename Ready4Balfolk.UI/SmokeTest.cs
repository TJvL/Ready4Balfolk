using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Cold start on a runner with software rendering is slow, but not this slow.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);

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

    /// <summary>
    /// Static so the watchdog survives: the only stack that could root it is the one blocked in
    /// the dispatcher main loop, and a collected timer never fires.
    /// </summary>
    private static Timer? _watchdog;

    public static int Run(AppBuilder builder, string[] args)
    {
        var mediaDirectory = ReadOption(args, "--smoke-test-media");

        var logFile = new FileInfo(Path.Combine(
            new ApplicationSettingsDirectory().DirectoryInfoRoot.FullName, "app.log"));

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

        failures.AddRange(ReadLoggedFailures(logFile, logOffset));

        int exitCode;
        if (failures.Count == 0)
        {
            Report($"passed: window opened, BASS, BASSFLAC and BASS_FX all loaded, "
                   + $"{decoded} media files decoded, log clean");
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
        // most able to hang on a background task. Swap the startup watchdog for one that carries
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

        // The logger caps the file by deleting it and starting over, so one that shrank is a fresh
        // file whose every line belongs to this run.
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
