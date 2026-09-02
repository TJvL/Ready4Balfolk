using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Primitives.Reactive.Concurrency;
using ReactiveUI.Reactive;
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
        // A fresh scheduler for a fresh dispatcher. The session gives every scenario its own
        // Application and its own dispatcher, but ReactiveUI's main thread scheduler is a static
        // that keeps pointing at the first one: from the second scenario on, everything the UI
        // observes on it is posted to a dispatcher that is never pumped again. The window comes up
        // and stays empty, the library never arrives, and the audio reads as unavailable.
        RxSchedulers.MainThreadScheduler = new AvaloniaScheduler(Dispatcher.UIThread, DispatcherPriority.Normal);

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

        Assert.Fail(
            $"Waited {PatienceLimit.TotalSeconds:0} seconds for {describedAs}, and it never happened.{Environment.NewLine}"
            + $"What was on screen:{Environment.NewLine}{WhatIsOnScreen()}{Environment.NewLine}"
            + $"What it logged:{Environment.NewLine}{WhatWasLogged()}");
    }

    /// <summary>The control with this automation id, on the window or on a dialog over it.</summary>
    public Control Find(string automationId)
    {
        // A visible one first: a panel that is hidden rather than removed is still in the tree, so
        // the artist of the track that is not playing is findable and says the last thing it said.
        var found = Everywhere()
            .SelectMany(root => Screen.AllWith(root, automationId))
            .OrderByDescending(control => control.IsEffectivelyVisible)
            .FirstOrDefault();

        Assert.True(found is not null, $"Nothing with the automation id {automationId} is on screen.");
        return found!;
    }

    /// <summary>Whether the thing with this automation id is on screen and visible.</summary>
    public bool IsShowing(string automationId) =>
        Everywhere()
            .SelectMany(root => Screen.AllWith(root, automationId))
            .Any(control => control.IsEffectivelyVisible);

    /// <summary>What the thing with this automation id says.</summary>
    public string TextOf(string automationId) => Screen.Says(Find(automationId));

    /// <summary>How far along the bar with this automation id has run.</summary>
    public double ProgressOf(string automationId) => ((ProgressBar)Find(automationId)).Value;

    /// <summary>The rows of the list with this automation id, in the order they are shown.</summary>
    public IReadOnlyList<string> RowsOf(string automationId)
    {
        var list = Find(automationId);
        return list is DataGrid ? Screen.GridRows(list) : Screen.Rows(list);
    }

    /// <summary>The row of a list or grid whose text contains this, or a failure saying what is there.</summary>
    public Control Row(string automationId, string containing)
    {
        var list = Find(automationId);
        // Visible ones only, or a click can land on the spare row a grid keeps to recycle: it
        // carries a copy of a real row's text and is nowhere on screen.
        var rows = list.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is ListBoxItem or DataGridRow)
            .Where(control => control.IsEffectivelyVisible)
            .ToList();

        var match = rows.FirstOrDefault(row => Screen.Says(row).Contains(containing, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            match is not null,
            $"No row of {automationId} mentions {containing}. It is showing: {string.Join(" / ", rows.Select(Screen.Says))}");

        return match!;
    }

    /// <summary>Clicks a control the way a mouse does, in the middle of it.</summary>
    public void Click(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        Assert.True(control.IsEffectivelyVisible, "The control clicked on is not on screen.");
        Assert.True(control.IsEffectivelyEnabled, "The control clicked on is disabled.");

        // The control's own window, which is not always the main one: a confirmation is a dialog
        // over it, and a click aimed at the window underneath lands on whatever is at those
        // coordinates there.
        var window = control.FindAncestorOfType<Window>() ?? Window;
        var middle = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var inWindow = control.TranslatePoint(middle, window)
                       ?? throw new InvalidOperationException("The control clicked on is not in its window.");

        window.MouseDown(inWindow, MouseButton.Left);
        window.MouseUp(inWindow, MouseButton.Left);
        Settle();
    }

    /// <summary>Clicks whatever carries this automation id.</summary>
    public void Click(string automationId) => Click(Find(automationId));

    /// <summary>Double taps a control, which is how a track is put in the queue.</summary>
    public void DoubleClick(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        Click(control);
        Click(control);
    }

    /// <summary>Types into whatever has the keyboard, character by character.</summary>
    public void Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var character in text)
        {
            Window.KeyTextInput(character.ToString());
        }

        Settle();
    }

    /// <summary>Everything the window is showing, for a scenario that has to say why it gave up.</summary>
    public string WhatIsOnScreen() =>
        string.Join(
            Environment.NewLine,
            Everywhere()
                .SelectMany(root => root.GetVisualDescendants().OfType<Control>())
                .Where(control => control.IsEffectivelyVisible)
                .Select(control => (Id: AutomationProperties.GetAutomationId(control), Text: Screen.Says(control)))
                .Where(seen => !string.IsNullOrEmpty(seen.Id) || !string.IsNullOrWhiteSpace(seen.Text))
                .Select(seen => $"  {(string.IsNullOrEmpty(seen.Id) ? "-" : seen.Id)}: {seen.Text}")
                .Distinct()
                .Take(40));

    /// <summary>The end of the application's own log, which says what it thinks went wrong.</summary>
    public static string WhatWasLogged()
    {
        var world = ScenarioApplication.World;
        if (world is null)
        {
            return "  (no world)";
        }

        var log = Path.Combine(world.DirectoryInfoRoot.FullName, "app.log");
        return File.Exists(log)
            ? string.Join(Environment.NewLine, File.ReadAllLines(log).TakeLast(15).Select(line => "  " + line))
            : "  (nothing logged)";
    }

    /// <summary>The window and whatever dialog is over it, which is where a control may be.</summary>
    private IEnumerable<Visual> Everywhere() =>
        new Visual[] { Window }.Concat(Window.OwnedWindows);

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
