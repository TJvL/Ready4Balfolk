using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI.Platform;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);
        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();

        // Bubbled, so whatever has the keyboard answers first and only what nothing wanted
        // reaches here. Tunnelled, this would take the space bar off a focused button and off an
        // open dropdown, which are the two places a space already means something.
        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Bubble);
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Main;

    /// <summary>The few things a DJ does with one hand, while the other is on a mixer.</summary>
    /// <remarks>
    /// The main screen only. Settings and help are read and typed into rather than played from,
    /// and the review queue has a keyboard of its own that these would fight with.
    /// </remarks>
    private void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel model || !model.Navigation.IsMainScreen)
        {
            return;
        }

        // The one control that has to be asked for rather than let answer for itself. A button
        // and a dropdown both mark a space as theirs on the way past, so bubbling has already
        // kept it from here; a text box does not, and a space that started the music out from
        // under somebody searching would make the search box unusable. Ctrl and an arrow is the
        // same case: in a box it walks the caret a word at a time.
        var typing = FocusManager?.GetFocusedElement() is TextBox;

        var plain = e.KeyModifiers is KeyModifiers.None;
        var control = e.KeyModifiers is KeyModifiers.Control;

        // An if-chain rather than a switch on the key, the way the review screen takes its own:
        // a switch over an enum is asked to populate every case, and this one has 250 of them.
        // The media keys are never handed back: no control in this window has a use for one.
        if (e.Key is Key.MediaPlayPause || (e.Key is Key.Space && plain && !typing))
        {
            Press(model.Playback.PlayPauseCommand);
        }
        else if (e.Key is Key.MediaNextTrack || (e.Key is Key.Right && control && !typing))
        {
            Press(model.Playback.NextOrClearCommand);
        }
        else if (e.Key is Key.MediaPreviousTrack || (e.Key is Key.Left && control && !typing))
        {
            Press(model.Playback.RestartCommand);
        }
        else if (e.Key is Key.F && control)
        {
            // The library rather than the dance list, because this is the box that searches for
            // something to play: asking for it while the other panel is up is asking for the
            // panel too.
            model.Navigation.IsDanceListMode = false;
            CatalogToolbar.FocusSearch();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    /// <summary>Runs a transport command, or nothing when there is nothing to run it on.</summary>
    /// <remarks>
    /// Asked rather than told. These commands refuse while there is no audio to act on, and a
    /// keystroke that arrives a moment early should do nothing at all rather than be pushed
    /// through a command that has already said it cannot run.
    /// </remarks>
    private static void Press(ICommand command)
    {
        if (command.CanExecute(parameter: null))
        {
            command.Execute(parameter: null);
        }
    }
}
