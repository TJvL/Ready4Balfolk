using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
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

    /// <summary>The control with this automation id inside one row, rather than anywhere.</summary>
    /// <remarks>
    /// A row of the review list carries the same ids as every other row, because they are one
    /// template: which dance box a scenario means is decided by which row it is looking at.
    /// </remarks>
    public static Control Within(Control row, string automationId)
    {
        ArgumentNullException.ThrowIfNull(row);

        var found = Screen.AllWith(row, automationId).FirstOrDefault(control => control.IsEffectivelyVisible);

        Assert.True(found is not null, $"This row has nothing with the automation id {automationId}.");
        return found!;
    }

    /// <summary>Clears a box inside one row and types this into it.</summary>
    public void TypeIntoWithin(Control row, string automationId, string text)
    {
        var box = Within(row, automationId);
        Click(box);

        var window = box.FindAncestorOfType<Window>() ?? Window;
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Type(text);
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

        // Scrolled to first, the way a person scrolls before they click: a control below the fold
        // is visible as far as the tree is concerned, and a click aimed at where it would be lands
        // on whatever is at those coordinates instead. The settings page is long enough to matter.
        control.BringIntoView();
        Settle();

        Assert.True(
            control.IsEffectivelyVisible,
            $"The control clicked on is not on screen.{Environment.NewLine}"
            + $"window visible: {Window.IsVisible}, bounds: {Window.Bounds}, control bounds: {control.Bounds}{Environment.NewLine}"
            + $"What was on screen:{Environment.NewLine}{WhatIsOnScreen()}");
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

    /// <summary>Whether these words are anywhere on screen.</summary>
    public bool SaysAnywhere(string text) =>
        Everywhere()
            .SelectMany(root => root.GetVisualDescendants().OfType<TextBlock>())
            .Where(block => block.IsEffectivelyVisible)
            .Any(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    /// <summary>The one thing on screen reading exactly this, for the lists that offer choices.</summary>
    /// <remarks>
    /// By text rather than by an id, because these are not controls somebody placed: the entries of
    /// a dropdown are the values themselves, and what the user picks is the word they can see.
    /// </remarks>
    public Control Offering(string text)
    {
        var offers = Everywhere()
            .SelectMany(root => root.GetVisualDescendants().OfType<Control>())
            .Where(control => control is ListBoxItem or ComboBoxItem or MenuItem)
            .Where(control => control.IsEffectivelyVisible)
            .ToList();

        var match = offers.FirstOrDefault(offer =>
            string.Equals(Screen.Says(offer), text, StringComparison.Ordinal));

        Assert.True(
            match is not null,
            $"Nothing on screen offers {text}. What is offered: {string.Join(" / ", offers.Select(Screen.Says))}");

        return match!;
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

    /// <summary>Clicks a box, clears what is in it, and types this instead.</summary>
    public void TypeInto(string automationId, string text)
    {
        var box = Find(automationId);
        Click(box);

        var window = box.FindAncestorOfType<Window>() ?? Window;
        window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Type(text);
    }

    /// <summary>Presses a key, for the choices a keyboard makes better than a mouse.</summary>
    public void Press(PhysicalKey key)
    {
        Window.KeyPressQwerty(key, RawInputModifiers.None);
        Window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle();
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

    /// <summary>How many screens the application has up for the room.</summary>
    public int ScreensShowing() => _startup.PresentationWindows.Count;

    /// <summary>Every window the application has open, which is where a control may be.</summary>
    /// <remarks>
    /// Not just the main one: a confirmation is a dialog over it, and the screen the dancers read is
    /// a window of its own on another monitor.
    /// </remarks>
    private IEnumerable<Visual> Everywhere()
    {
        var windows = new Visual[] { Window }
            .Concat(Window.OwnedWindows)
            .Concat(_startup.PresentationWindows)
            .ToList();

        // What a dropdown or a context menu is showing hangs off the popup rather than off the
        // window: opened for real, it is a top level of its own, and the entries a user is choosing
        // between are nowhere in the window's tree.
        var offered = windows
            .SelectMany(window => window.GetVisualDescendants().OfType<Popup>())
            .Where(popup => popup.IsOpen)
            .Select(popup => popup.Child)
            .OfType<Visual>();

        return windows.Concat(offered);
    }

    /// <summary>Teardown, while what it should let go of is being worked out.</summary>
    /// <summary>Unhooks what startup wired up, and leaves everything else to the scenario's own application.</summary>
    /// <remarks>
    /// <para>
    /// The window is left open and the container is left alone. Every scenario builds its own, and
    /// they cost nothing but memory for the length of a run; disposing the container instead took
    /// ReactiveUI's registrations down with it, and the next scenario could not resolve so much as a
    /// property notifier.
    /// </para>
    /// <para>
    /// The audio device is not freed either, and does not need to be: BASS is initialised per
    /// process, and the application now carries on from a device that is already up rather than
    /// reading it as a failure. Freeing it here instead broke the scenario that came next, which
    /// had subscribed to what was being disposed underneath it.
    /// </para>
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        _startup.Dispose();
        return ValueTask.CompletedTask;
    }
}
