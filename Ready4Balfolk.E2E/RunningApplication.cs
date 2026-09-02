using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI;

namespace Ready4Balfolk.E2E;

/// <summary>The application, running, with a window a scenario can click on.</summary>
/// <remarks>
/// Started the way the desktop start does it, through the real <c>ApplicationStartup</c> and a real
/// desktop lifetime, because everything that makes the window usable hangs off that: the stores are
/// loaded, the wizard decides whether to show itself, the window state is restored and the embedded
/// server follows the settings. The headless platform supplies no lifetime of its own, so the one
/// thing this class does that a user does not is construct it.
/// </remarks>
public sealed class RunningApplication : IAsyncDisposable
{
    /// <summary>How long a step waits for something the application does on its own.</summary>
    private static readonly TimeSpan PatienceLimit = TimeSpan.FromSeconds(10);

    private readonly ApplicationStartup _startup;

    private RunningApplication(ApplicationStartup startup, MainWindow window)
    {
        _startup = startup;
        Window = window;
    }

    /// <summary>The main window, which is what a scenario clicks on.</summary>
    public MainWindow Window { get; }

    internal static RunningApplication Start()
    {
        var application = (App)Application.Current!;
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            // Nothing in a scenario asks the process to end, and the exit confirmation is a dialog
            // with nobody to answer it, so shutdown is never anything but explicit.
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        var startup = App.Services.GetRequiredService<ApplicationStartup>();
        startup.Run(lifetime, application);

        var window = (MainWindow)lifetime.MainWindow!;
        window.Show();

        var running = new RunningApplication(startup, window);
        running.Settle();
        return running;
    }

    /// <summary>Lets everything the last step started run to a standstill.</summary>
    /// <remarks>
    /// The layout pass is not incidental. A click is aimed at the middle of a control, and a
    /// control that has appeared but not been laid out yet is still sitting at nought by nought.
    /// </remarks>
    public void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    /// <summary>Waits for something the application does on its own, like a track ending.</summary>
    public async Task WaitUntil(Func<bool> what, string describedAs)
    {
        ArgumentNullException.ThrowIfNull(what);

        var deadline = DateTime.UtcNow + PatienceLimit;
        while (DateTime.UtcNow < deadline)
        {
            Settle();
            if (what())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Waited {PatienceLimit.TotalSeconds:0} seconds for {describedAs}, and it never happened.");
    }

    /// <summary>The one control with this name, or a failure naming what was looked for.</summary>
    public T Find<T>(string name)
        where T : Control
    {
        var found = Window.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));

        Assert.True(found is not null, $"No {typeof(T).Name} named {name} is on screen.");
        return found!;
    }

    /// <summary>Clicks a control the way a mouse does, in the middle of it.</summary>
    public void Click(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        Assert.True(control.IsEffectivelyVisible, "The control clicked on is not on screen.");
        Assert.True(control.IsEffectivelyEnabled, "The control clicked on is disabled.");

        var middle = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var inWindow = control.TranslatePoint(middle, Window)
                       ?? throw new InvalidOperationException("The control clicked on is not in the window.");

        Window.MouseDown(inWindow, MouseButton.Left);
        Window.MouseUp(inWindow, MouseButton.Left);
        Settle();
    }

    /// <summary>Lets go of the world's directory, which is all teardown is for.</summary>
    /// <remarks>
    /// The window is left open. Closing it is a scenario step, with a confirmation dialog behind it
    /// and nobody here to answer, and the application instance goes at the end of the scenario
    /// anyway. What has to happen is the audio device, the two SQLite files and the embedded server
    /// letting go, or the next scenario inherits them and this one's directory cannot be deleted.
    ///
    /// Asynchronously, because the container holds the web server, which is IAsyncDisposable: a
    /// synchronous dispose of the provider throws rather than draining Kestrel.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        _startup.Dispose();

        switch (App.Services)
        {
            case IAsyncDisposable container:
                await container.DisposeAsync();
                break;
            case IDisposable container:
                container.Dispose();
                break;
            default:
                break;
        }
    }
}
