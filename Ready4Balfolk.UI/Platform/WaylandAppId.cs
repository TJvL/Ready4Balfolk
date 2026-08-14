using System;
using System.Reflection;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.UI.Platform;

/// <summary>
/// Gives a Wayland window the <c>app_id</c> that ties it to its desktop entry, which the Wayland
/// backend never sets by itself.
/// </summary>
/// <remarks>
/// <para>
/// The X11 backend takes the equivalent through <c>X11PlatformOptions.WmClass</c>. Wayland has no
/// counterpart: <c>WaylandPlatformOptions</c> carries no app id, and Avalonia never calls
/// <c>xdg_toplevel.set_app_id</c>, so every window reports an empty class. Without it a taskbar
/// shows the window with no icon and never groups it under the launcher entry, and compositor
/// window rules have nothing to match on but the title.
/// </para>
/// <para>
/// The underlying binding does expose the request, so the toplevel is reached by reflection and
/// asked directly. That means private field names, so every step is checked and a miss is logged
/// and shrugged off rather than thrown: an app id is worth a taskbar icon, not a failed startup.
/// Drop this the moment Avalonia offers the setting.
/// </para>
/// </remarks>
internal static class WaylandAppId
{
    /// <summary>
    /// Matches the desktop entry basename and the Flatpak application id, which is what desktops
    /// compare against to pair a window with its launcher. All three have to stay in step.
    /// </summary>
    internal const string Value = "io.github.tjvl.Ready4Balfolk";

    private const string WindowImplTypeName = "Avalonia.Wayland.WindowImpl";
    private const string SurfaceProxyField = "_surfaceProxy";
    private const string ProxyTargetField = "_target";
    private const string ToplevelField = "_xdgTopLevel";
    private const string WorkerClientProperty = "Client";
    private const string PostMethod = "PostWithCommit";
    private const string SetAppIdMethod = "SetAppId";

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _hasReported;

    /// <summary>
    /// Applies the app id to <paramref name="window"/>, and does nothing on any backend other than
    /// Wayland. Call it before the window is shown: a compositor reads the app id when the surface
    /// is mapped, and one that arrives later leaves the initial class empty, which is what static
    /// window rules match against.
    /// </summary>
    internal static void Apply(Window window)
    {
        var impl = window.PlatformImpl;
        if (impl is null || impl.GetType().FullName != WindowImplTypeName)
        {
            return;
        }

        try
        {
            // The window holds a proxy rather than the toplevel itself: Avalonia 12 keeps every
            // Wayland protocol object on its own worker thread and marshals calls to it.
            var proxy = impl.GetType().GetField(SurfaceProxyField, Instance)?.GetValue(impl);
            var target = proxy?.GetType().GetField(ProxyTargetField, Instance)?.GetValue(proxy);
            var toplevel = target?.GetType().GetField(ToplevelField, Instance)?.GetValue(target);
            var setAppId = toplevel?.GetType().GetMethod(SetAppIdMethod, Instance, [typeof(string)]);

            var client = impl.GetType().GetProperty(WorkerClientProperty, Instance)?.GetValue(impl);
            var post = client?.GetType().GetMethod(PostMethod, Instance, [typeof(Action)]);

            if (setAppId is null || post is null)
            {
                Report("Wayland app id could not be applied: the backend no longer exposes the toplevel where this expects it.");
                return;
            }

            Action request = () => SetOnWorkerThread(setAppId, toplevel!);
            post.Invoke(client, [request]);
        }
        catch (Exception exception)
        {
            Report($"Wayland app id could not be applied: {exception.Message}");
        }
    }

    private static void SetOnWorkerThread(MethodInfo setAppId, object toplevel)
    {
        // Runs on the Wayland worker thread, where an escaping exception has nothing above it to
        // catch it and would take the process down over a cosmetic detail.
        try
        {
            setAppId.Invoke(toplevel, [Value]);
        }
        catch (Exception exception)
        {
            Report($"Wayland app id was rejected by the compositor connection: {exception.Message}");
        }
    }

    /// <summary>
    /// Logs the first failure only. Every window goes through here, so a backend change would
    /// otherwise repeat the same line once per window and again for each dialog.
    /// </summary>
    private static void Report(string message)
    {
        if (_hasReported)
        {
            return;
        }

        _hasReported = true;
        _ = App.Services?.GetService<ILoggerService>()?.WarningAsync(message);
    }
}
