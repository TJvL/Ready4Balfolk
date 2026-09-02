using System;
using System.Diagnostics;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI;

public static class Program
{
    internal static readonly Stopwatch StartupStopwatch = new();

    /// <summary>
    /// True when started with <c>--smoke-test</c>: the app comes up for a CI check rather than for
    /// a user, so it must be able to shut itself down and must not depend on an audio device.
    /// </summary>
    internal static bool IsSmokeTest { get; private set; }

    private static LogLevel _minimumLogLevel = LogLevel.Info;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        StartupStopwatch.Start();

        // ReSharper disable once RedundantAssignment
        var isDebug = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);
#if DEBUG
        isDebug = true;
#endif
        if (isDebug)
        {
            _minimumLogLevel = LogLevel.Debug;
        }

        IsSmokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);

        try
        {
            return IsSmokeTest
                ? SmokeTest.Run(BuildAvaloniaApp(), args)
                : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            _ = App.Services?.GetService<ILoggerService>()?.CriticalAsync("Fatal startup exception", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => ApplicationComposition.Configure(
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                // Takes over from the X11 backend selected above whenever a Wayland compositor is
                // present, and leaves it in place otherwise. Must follow UsePlatformDetect.
                .UseWaylandWithFallback()
                // The X11 backend exports a global menu over DBus to com.canonical.AppMenu.Registrar,
                // which only exists under Unity-style panels. The app has a toolbar and no native menu
                // bar, so the export buys nothing and throws on every other desktop.
                .With(new X11PlatformOptions
                {
                    UseDBusMenu = false,
                    // Desktops match a window to its launcher entry by comparing WM_CLASS to the
                    // desktop file basename, and without that the taskbar shows a window with no
                    // icon next to a launcher that has one. The Wayland equivalent has no option to
                    // set it through, so each window applies it for itself: see WaylandAppId.
                    WmClass = WaylandAppId.Value
                }),
            new ApplicationOptions
            {
                UseNoSoundDevice = IsSmokeTest,
                MinimumLogLevel = _minimumLogLevel
            });
}
