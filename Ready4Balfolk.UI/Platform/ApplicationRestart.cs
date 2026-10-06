using System;
using System.Diagnostics;
using System.IO;

namespace Ready4Balfolk.UI.Platform;

/// <summary>Starts a fresh copy of the application and ends this one.</summary>
/// <remarks>
/// The new copy is started through a shell that waits a moment first, so that this one has let go
/// of what it holds (the web server's port above all) before the next one asks for it. Each
/// platform gets its own shell: Windows has no <c>setsid</c>, and starting it there threw before
/// <see cref="Environment.Exit" /> was reached, so answering yes to the restart did nothing.
/// </remarks>
internal static class ApplicationRestart
{
    public static void Run()
    {
        if (Environment.ProcessPath is { } exePath)
        {
            Process.Start(StartInfo(exePath, OperatingSystem.IsWindows()));
        }

        Environment.Exit(0);
    }

    /// <summary>What starts the next copy on this platform, apart so a test can read it.</summary>
    internal static ProcessStartInfo StartInfo(string exePath, bool onWindows) => onWindows
        ? new ProcessStartInfo
        {
            // cmd keeps a command line that does not open with a quote exactly as written, and
            // ping is the wait that needs no console: timeout refuses to run without one.
            FileName = "cmd.exe",
            Arguments = $"/c ping -n 2 127.0.0.1 >nul & start \"\" \"{exePath}\"",
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        }
        : new ProcessStartInfo
        {
            // A new session, fully detached from this one, so it outlives the exit below.
            FileName = "setsid",
            Arguments = $"--fork /bin/sh -c \"sleep 0.3 && '{exePath}'\"",
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false
        };
}
