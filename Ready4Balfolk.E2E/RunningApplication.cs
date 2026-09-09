using System.IO.Abstractions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Settings;
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

    /// <summary>How long something has to stay true to count as settled rather than as a flicker.</summary>
    /// <remarks>
    /// Longer than the slowest throttle a panel is fed through, which is three tenths of a second,
    /// so a pass that is already on its way has landed before this answers.
    /// </remarks>
    private static readonly TimeSpan HoldsFor = TimeSpan.FromMilliseconds(400);

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

    /// <summary>Waits, without letting go of the step, for something a moment away.</summary>
    private void WaitFor(Func<bool> what, string complaint)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            Settle();
            if (what())
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"{complaint}{Environment.NewLine}What was on screen:{Environment.NewLine}{WhatIsOnScreen()}");
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

    /// <summary>Waits for something to become true and stay true, rather than to flicker true.</summary>
    /// <remarks>
    /// A panel fed from throttled sources is brought up to date several times over as they arrive,
    /// so a step that waits for one to be ready can be answered by the first of those passes and
    /// then act on a screen the next pass is about to change under it. This asks the same question
    /// on every pass and only answers once it has held for longer than the throttles it is waiting
    /// out.
    /// </remarks>
    public async Task WaitUntilItStays(Func<bool> what, string describedAs)
    {
        ArgumentNullException.ThrowIfNull(what);

        var deadline = DateTime.UtcNow + PatienceLimit;
        DateTime? since = null;

        while (DateTime.UtcNow < deadline)
        {
            Settle();

            if (!what())
            {
                since = null;
            }
            else
            {
                since ??= DateTime.UtcNow;
                if (DateTime.UtcNow - since.Value >= HoldsFor)
                {
                    return;
                }
            }

            await Task.Delay(20);
        }

        Assert.Fail(
            $"Waited {PatienceLimit.TotalSeconds:0} seconds for {describedAs}, and it never held.{Environment.NewLine}"
            + $"What was on screen:{Environment.NewLine}{WhatIsOnScreen()}{Environment.NewLine}"
            + $"What it logged:{Environment.NewLine}{WhatWasLogged()}");
    }

    /// <summary>The control with this automation id, on the window or on a dialog over it.</summary>
    public Control Find(string automationId)
    {
        var found = LookFor(automationId);

        Assert.True(found is not null, $"Nothing with the automation id {automationId} is on screen.");
        return found!;
    }

    /// <summary>The control with this automation id, or nothing where there is none yet.</summary>
    /// <remarks>
    /// What <see cref="Find"/> is built on, and what the questions a step is allowed to ask while
    /// waiting are built on: a wait that fails the scenario the first time the answer is no is not
    /// a wait at all.
    /// </remarks>
    private Control? LookFor(string automationId) =>
        // A visible one first: a panel that is hidden rather than removed is still in the tree, so
        // the artist of the track that is not playing is findable and says the last thing it said.
        Everywhere()
            .SelectMany(root => Screen.AllWith(root, automationId))
            .OrderByDescending(control => control.IsEffectivelyVisible)
            .FirstOrDefault();

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

    /// <summary>Whether the thing with this automation id is showing inside one row.</summary>
    /// <remarks>
    /// The negative of <see cref="Within"/>, which fails when nothing is there: a row that shows one
    /// button in place of another has to be readable both ways round.
    /// </remarks>
    public static bool IsShowingWithin(Control row, string automationId)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Screen.AllWith(row, automationId).Any(control => control.IsEffectivelyVisible);
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

    /// <summary>Whether the thing with this automation id would take the keyboard if offered it.</summary>
    /// <remarks>
    /// On screen is not the same as ready. A control that has only just appeared, because what it
    /// is bound to has only just become true, is in the tree and drawn before it will answer
    /// <see cref="InputElement.Focus" />, so a scenario that waits for it to be visible and then
    /// gives it the keyboard races the layout pass and loses on a slower machine than the one it
    /// was written on.
    /// Nothing on screen answers no rather than failing the scenario, because this is a question a
    /// wait asks over and over while a control is on its way in: not yet is the answer it is for.
    /// </remarks>
    public bool CanTakeTheKeyboard(string automationId) =>
        LookFor(automationId) is { } control
        && control.IsEffectivelyVisible
        && control.IsEffectivelyEnabled
        && control.Focusable;

    /// <summary>What the thing with this automation id says.</summary>
    public string TextOf(string automationId) => Screen.Says(Find(automationId));

    /// <summary>What a screen reader would call the thing with this automation id.</summary>
    /// <remarks>
    /// Asked of the automation peer rather than read off the attached property, because the peer
    /// is what a screen reader actually asks: a name set on the wrong element of the pair reads
    /// back fine from the property and is still nothing to anybody listening.
    /// </remarks>
    public string NameOf(string automationId) => NameOf(Find(automationId));

    /// <summary>What a screen reader would call this control.</summary>
    public static string NameOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? string.Empty;
    }

    /// <summary>Gives a control the keyboard, and fails if it will not take it.</summary>
    /// <remarks>
    /// Where Tab would leave it. A control that refuses focus simply returns false here rather
    /// than throwing, so the refusal is what is asserted: that is the whole of the difference
    /// between a screen a keyboard can drive and one it cannot.
    /// </remarks>
    public void GiveTheKeyboardTo(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.BringIntoView();
        Settle();

        Assert.True(
            control.Focus(),
            $"The control would not take the keyboard.{Environment.NewLine}"
            + $"What was on screen:{Environment.NewLine}{WhatIsOnScreen()}");

        Settle();
    }

    /// <summary>Gives the keyboard to whatever carries this automation id.</summary>
    public void GiveTheKeyboardTo(string automationId) => GiveTheKeyboardTo(Find(automationId));

    /// <summary>What has the keyboard, which is the only thing Tab is ever about.</summary>
    public Control? WhatHasTheKeyboard() => Window.FocusManager?.GetFocusedElement() as Control;

    /// <summary>Whether the keyboard is on this control, or on a part it is drawn from.</summary>
    /// <remarks>
    /// A box takes the keyboard on the presenter inside it rather than on itself, so a reference
    /// check alone would say the caret is nowhere near a box the caret is in.
    /// </remarks>
    public bool TheKeyboardIsOn(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return WhatHasTheKeyboard() is { } focused
               && (focused == control || control.IsVisualAncestorOf(focused));
    }

    /// <summary>What a scenario calls the thing the keyboard is on, for the walks that report one.</summary>
    public string WhateverHasTheKeyboardIsCalled()
    {
        if (WhatHasTheKeyboard() is not { } focused)
        {
            return "nothing";
        }

        // Its id where it has one, and the name a screen reader would read where it has not: the
        // buttons of a row carry ids, the two-state preview button carries only a name.
        var id = AutomationProperties.GetAutomationId(focused);
        return string.IsNullOrEmpty(id) ? NameOf(focused) : id;
    }

    /// <summary>How far along the bar with this automation id has run.</summary>
    public double ProgressOf(string automationId) => ((ProgressBar)Find(automationId)).Value;

    /// <summary>The rows of the list with this automation id, in the order they are shown.</summary>
    public IReadOnlyList<string> RowsOf(string automationId)
    {
        var list = Find(automationId);
        return list is DataGrid ? Screen.GridRows(list) : Screen.Rows(list);
    }

    /// <summary>What a control says, for the assertions that read one directly.</summary>
    public static string Says(Control control) => Screen.Says(control);

    /// <summary>Whether all the words a text block draws fit in the room it was given.</summary>
    /// <remarks>
    /// Its bounds are no use for this. A line too long for the space it has is arranged at the
    /// width of that space and simply draws past both edges of it, so the control measures as
    /// fitting while half of what it says is off the screen. What is compared is the text itself
    /// against the room it was laid out in.
    /// </remarks>
    public static bool WordsFitWhereTheyAreDrawn(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var text = control as TextBlock
                   ?? throw new InvalidOperationException("That control draws no text of its own.");

        // A pixel of slack: a line that fills its room exactly is measured either way round.
        return text.TextLayout.Width <= text.Bounds.Width + 1;
    }

    /// <summary>Whether the window this is in is showing the whole of what it holds.</summary>
    /// <remarks>
    /// The property rather than the symptom, and deliberately so: a headless window has no title
    /// bar, so a height typed into a view leaves room here that the same number does not leave on a
    /// desk, and measuring the text would call a clipped dialog fine.
    /// Two questions, because the first on its own proves less than it reads like. The window is
    /// the height its content asked for, so nothing was cut to fit a number somebody typed into the
    /// view. And nothing in it is scrolling, because a control given a height of its own asks for
    /// that height and gets it, and the window around it then measures as an exact fit over a
    /// message showing half of itself.
    /// A confirmation past its MaxHeight looks the same way round, and this answers no there,
    /// which is the limit of what it proves: that the whole of the dialog is on screen, never that
    /// a message long enough to reach the cap could be. Past the cap the content asks for the cap,
    /// so the heights agree while the question is behind a scroll bar.
    /// </remarks>
    public bool TheWindowShowingItShowsTheWholeOfWhatItHolds(string automationId)
    {
        var window = WindowShowing(automationId);
        var held = window.Content as Control
                   ?? throw new InvalidOperationException($"The window showing {automationId} holds no control.");

        // A pixel of slack, for the same reason the text measurement above takes one.
        var theWindowIsTheHeightOfIt = Math.Abs(window.ClientSize.Height - held.DesiredSize.Height) <= 1;

        var nothingIsScrolling = window.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(scroller => scroller.IsEffectivelyVisible)
            .All(scroller => scroller.Extent.Height <= scroller.Viewport.Height + 1);

        return theWindowIsTheHeightOfIt && nothingIsScrolling;
    }

    /// <summary>How tall the window this is in has made itself.</summary>
    public double HowTallTheWindowShowingItIs(string automationId) => WindowShowing(automationId).ClientSize.Height;

    /// <summary>The window something is in, which for anything in a dialog is the dialog.</summary>
    /// <remarks>
    /// It refuses rather than falling back on the main window. A flyout, a dropdown or anything
    /// else in the overlay layer is in no window of its own, and answering "the main one" there
    /// would measure the main window against the main window's content, which agree for reasons
    /// that have nothing to do with the dialog under test.
    /// </remarks>
    private Window WindowShowing(string automationId) =>
        Find(automationId).FindAncestorOfType<Window>()
        ?? throw new InvalidOperationException(
            $"{automationId} is in no window of its own, so there is no window here to measure.");

    /// <summary>Every row a list is showing, for the assertions that are about all of them.</summary>
    public IReadOnlyList<Control> Rows(string automationId) =>
        Find(automationId).GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is ListBoxItem or DataGridRow)
            .Where(control => control.IsEffectivelyVisible)
            .ToList();

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

        // A control that is briefly disabled is one the application is still busy with: a command
        // disables itself while it runs, so on a slow machine the button a person is about to press
        // is unavailable for a moment. Waiting is what the person does.
        WaitFor(() => control.IsEffectivelyEnabled, "The control clicked on stayed disabled.");

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

    /// <summary>Right clicks a control, which is how a row's own menu is opened.</summary>
    public void RightClick(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.BringIntoView();
        Settle();

        var window = control.FindAncestorOfType<Window>() ?? Window;
        var middle = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var inWindow = control.TranslatePoint(middle, window)
                       ?? throw new InvalidOperationException("The control clicked on is not in its window.");

        window.MouseDown(inWindow, MouseButton.Right);
        window.MouseUp(inWindow, MouseButton.Right);
        Settle();
    }

    /// <summary>Whether these words are anywhere on screen.</summary>
    public bool SaysAnywhere(string text) =>
        Everywhere()
            .SelectMany(root => root.GetVisualDescendants().OfType<TextBlock>())
            .Where(block => block.IsEffectivelyVisible)
            .Any(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    /// <summary>Whether these words appear anywhere on screen, inside whatever else is written.</summary>
    public bool SeesAnywhere(string text) =>
        Everywhere()
            .SelectMany(root => root.GetVisualDescendants().OfType<TextBlock>())
            .Where(block => block.IsEffectivelyVisible)
            .Any(block => block.Text?.Contains(text, StringComparison.Ordinal) == true);

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

    /// <summary>Makes the window this wide, for what happens when there is not much room.</summary>
    /// <remarks>
    /// A DJ's window is whatever their screen gives them, and every panel in it is a fraction of
    /// that: what fits on the machine a scenario was written on is not what fits on theirs.
    /// </remarks>
    public void TheWindowIsThisWide(double width)
    {
        Window.Width = width;
        Settle();
    }

    /// <summary>Moves and resizes the window, the way a DJ does over the course of an evening.</summary>
    /// <remarks>
    /// A size and a place the world did not lay down, so what is written down at closing time can
    /// only have come from the window. Asserting what the run seeded is green whether or not
    /// anything ever read the window.
    /// </remarks>
    public void TheDjMovesAndResizesTheWindow(PixelPoint to, double width, double height)
    {
        Window.Position = to;
        Window.Width = width;
        Window.Height = height;
        Settle();
    }

    /// <summary>Moves and resizes a screen, the way a DJ does before pointing it at a projector.</summary>
    /// <remarks>
    /// A place and a size the world did not lay down, for the same reason
    /// <see cref="TheDjMovesAndResizesTheWindow"/> moves the main one: what closing time writes down
    /// can then only have come from reading the screen back.
    /// </remarks>
    public void TheDjMovesAndResizesTheScreen(int index, PixelPoint to, double width, double height)
    {
        var screen = _startup.PresentationWindows[index];
        screen.Position = to;
        screen.Width = width;
        screen.Height = height;
        Settle();
    }

    /// <summary>Whether a screen is currently showing without its border and title bar.</summary>
    public bool ScreenIsBorderless(int index) => _startup.PresentationWindows[index].IsBorderless;

    /// <summary>Puts the evening further along, for the things that are minutes or hours away.</summary>
    public static void TimePassed(TimeSpan howLong) => ScenarioApplication.Clock.MoveOn(howLong);

    /// <summary>Says which file the DJ picks the next time something asks them for one.</summary>
    public static void TheDjWillPick(string path) => ScenarioApplication.Pickers.TheyWillPick(path);

    /// <summary>A newer dance list lands, without a button being pressed to ask for it.</summary>
    /// <remarks>
    /// Through the store the Import button goes through, and not through the button, because
    /// pressing Import puts the keyboard on the Import button. The moment worth a scenario is the
    /// other one: the DJ asked for a list a second ago, their hands are back in the panel, and it
    /// lands under them. Awaited, so what follows is the panel with the newer list already in it.
    /// </remarks>
    public async Task ANewerDanceListArrivesFrom(string path)
    {
        var update = await App.Services.GetRequiredService<IDanceListStore>()
            .UpdateFromFileAsync(new FileSystem().FileInfo.New(path));

        Assert.True(
            update.Outcome == DanceListUpdateOutcome.Updated,
            $"The list from the file was not taken: {update.Problem}");

        Settle();
    }

    /// <summary>Changes a setting the way the equalizer and the web server do: off the UI thread.</summary>
    /// <remarks>
    /// Neither of those publishes from a click, so a scenario reaching for the store directly, on a
    /// thread of its own, is the only way to raise a settings change the way they really do.
    /// </remarks>
    public static async Task ASettingChangesFromABackgroundThread(
        Func<ApplicationSettings, ApplicationSettings> change)
    {
        var store = App.Services.GetRequiredService<ISettingsStore>();
        await Task.Run(() => store.UpdateAsync(change));
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
    public void Press(PhysicalKey key) => Press(key, RawInputModifiers.None);

    /// <summary>Presses a key with something held down, for the shortcuts that need one.</summary>
    public void Press(PhysicalKey key, RawInputModifiers modifiers)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
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

    /// <summary>The theme the application actually has applied, not what the setting asks for.</summary>
    /// <remarks>
    /// <c>App.ApplyTheme</c> is what turns a setting into this: it sets
    /// <see cref="Application.RequestedThemeVariant"/>, which is what restyles every window
    /// already open. Reading that back, rather than the setting, is what makes a scenario prove the
    /// subscription ran rather than merely that the store holds the new value.
    /// </remarks>
    public static ThemeVariant CurrentTheme() => Application.Current!.RequestedThemeVariant ?? ThemeVariant.Default;

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
    /// <summary>Lets the scenario finish, and leaves what it was using alone.</summary>
    /// <remarks>
    /// The process ends a moment after this, which closes every file and frees every device, so
    /// there is nothing here worth racing for. Disposing the container instead put a stopwatch on
    /// everything still in flight: a task completing a moment later wrote to a logger that had been
    /// disposed, and the run died on that rather than on anything a scenario asserted.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        _startup.Dispose();
        Settle();

        return ValueTask.CompletedTask;
    }
}
