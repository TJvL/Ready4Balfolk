using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>
/// The review queue, driven from the keyboard.
/// </summary>
/// <remarks>
/// Two thousand mouse trips is the difference between an evening and never, so a row is answered
/// without the hands leaving the keys: selecting one puts the caret where the typing has to start,
/// Tab walks its boxes and then its buttons and leaves at the end of them, Enter answers the row,
/// Shift+Enter answers the folder and Ctrl+Z takes an answer back.
/// </remarks>
public partial class ReviewView : ReactiveUserControl<ReviewViewModel>
{
    public ReviewView()
    {
        InitializeComponent();

        // Tunnelled, because these keys belong to the queue before they belong to a text box: Tab
        // would otherwise carry the focus out of the row, and Enter would be swallowed entirely.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // The rules panel hangs over this view but is not part of it: it owns its own boxes,
        // checkbox and buttons, so a key that started there belongs to it, never to the queue
        // underneath. Checked before anything else, because the queue's keys reach from Tab to
        // Ctrl+Z and every one of them would otherwise be claimed here first and never reach the
        // panel.
        if (e.Source is Visual source && (source == RulesPanel || RulesPanel.IsVisualAncestorOf(source)))
        {
            return;
        }

        if (ViewModel is not { Selected: { } selected })
        {
            return;
        }

        // The list of names owns the arrows and Enter while it is open: walking what it found and
        // taking one is the whole point of it being there. The row that owns the focused box, not
        // the selected row: clicking into a box does not move the selection, and the picker that
        // opened belongs to where the typing is.
        var typing = (e.Source as Control)?.DataContext as ReviewRowViewModel ?? selected;

        // A focused button owns Enter and Space, because that is how a button is pressed. The
        // queue's own keys would otherwise answer a row while the DJ was pressing something else,
        // and the button they were on would never fire at all.
        if (e.Source is Button && e.Key is Key.Enter or Key.Space && e.KeyModifiers is KeyModifiers.None)
        {
            return;
        }

        // After this keystroke has done its work, including opening the picker: the overlay adds
        // no layout extent, so a bottom-of-viewport row's list is clipped with provably no room
        // below unless the scroller is asked to show the space it paints into.
        Dispatcher.UIThread.Post(() => BringPickerIntoView(typing), DispatcherPriority.Background);

        if (typing.IsPickerOpen)
        {
            if (e.Key is Key.Up or Key.Down)
            {
                typing.MoveHighlight(e.Key is Key.Up ? -1 : 1);
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Enter)
            {
                e.Handled = typing.TakeHighlighted();
                return;
            }

            if (e.Key is Key.Escape)
            {
                typing.ClosePicker();
                e.Handled = true;
                return;
            }
        }

        // Undo, on the row the caret is in rather than on the selected row, and only while that row
        // has been answered: on a row still being typed into, Ctrl+Z belongs to the box under the
        // caret, and taking the selected row's answer back instead would undo somebody else's work
        // and eat the keystroke the typist meant.
        if (e.Key is Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) && typing.IsApproved)
        {
            ViewModel.WithdrawCommand.Execute(typing).Subscribe();
            e.Handled = true;
            return;
        }

        // An if-chain rather than a switch on the key: a switch over an enum invites "populate
        // every case", and this one has 250 of them.
        if (e.Key is Key.Tab)
        {
            // Through this row before anything else: its three boxes, and then the buttons that
            // answer with what was typed into them. Past the last of those the key is handed back
            // rather than wrapped, or the row would be somewhere the keyboard can get into and
            // never out of, with every button on it reachable by pointer alone.
            e.Handled = MoveAlongRow(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            return;
        }

        if (e.Key is Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                ViewModel.ApproveFolderCommand.Execute(selected).Subscribe();
            }
            else
            {
                ViewModel.ApproveCommand.Execute(selected).Subscribe();
            }

            FocusMovedRow();
        }
        else if (e.Key is Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // A file BASS will not open is the ordinary case here rather than the exceptional one:
            // this queue is where those land, so the DJ is told rather than shown a closed window.
            Handlers.Run(UiStrings.Review_PreviewFailed, () => ViewModel.TogglePreviewAsync(selected));
        }
        else if (e.Key is Key.Escape)
        {
            Handlers.Run(UiStrings.Review_StopPreviewFailed, ViewModel.StopPreviewAsync);
        }
        else if (e.Key is Key.Left or Key.Right && ViewModel.IsPreviewing)
        {
            // Only while something is playing, so they stay ordinary editing keys the rest of the
            // time: a typo in the middle of a title still has to be reachable.
            Handlers.Run(UiStrings.Review_SeekPreviewFailed, () => ViewModel.SeekByAsync(TimeSpan.FromSeconds(e.Key is Key.Left ? -5 : 5)));
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            ViewModel.Step(e.Key is Key.Up ? -1 : 1);
            FocusMovedRow();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Puts the caret where the typing starts on the row the keys just moved to.
    /// </summary>
    /// <remarks>
    /// Only after a key, never on every selection change: clicking into a title has to leave the
    /// caret in the title rather than throwing it back to the first empty field.
    /// </remarks>
    private void FocusMovedRow() =>
        // Once the container exists: a row reached by answering the one above it is realised in
        // this same pass, and focusing something not yet there does nothing at all.
        Dispatcher.UIThread.Post(FocusFirstEmptyField, DispatcherPriority.Background);

    private void FocusFirstEmptyField()
    {
        var fields = FieldsOfSelectedRow();
        if (fields.Count == 0)
        {
            return;
        }

        // The first thing missing, or the first field when nothing is missing. Either way the
        // answer can be typed without reaching for the pointer.
        (fields.FirstOrDefault(field => string.IsNullOrWhiteSpace(TextOf(field))) ?? fields[0]).Focus();
    }

    /// <summary>
    /// Moves the keyboard along the row it is already in, and lets it out at either end.
    /// </summary>
    /// <remarks>
    /// Answers false for every keystroke that is not a step along this row, which hands Tab back
    /// to the window: from anywhere else on the screen, and from either end of the row itself.
    /// </remarks>
    private bool MoveAlongRow(int direction)
    {
        if (ViewModel?.Selected is not { } selected
            || Queue.ContainerFromItem(selected) is not { } row
            || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }

        // Only the row the keyboard is standing in. The rules panel hangs over this same view, and
        // a Tab pressed in there belongs to the panel rather than to whichever row is selected
        // behind it.
        if (row != focused && !row.IsVisualAncestorOf(focused))
        {
            return false;
        }

        var stops = StopsOf(row);
        if (stops.Count == 0)
        {
            return false;
        }

        // The focus sits on a part inside a control rather than on the control itself, so a
        // reference check alone would never find where the keyboard is.
        var at = stops.FindIndex(stop => stop == focused || stop.IsVisualAncestorOf(focused));
        if (at < 0)
        {
            // On the row itself, which is where Tab arrives when it reaches the list: forwards it
            // steps into the row, backwards it carries on out to whatever came before the list.
            return direction > 0 && MoveTo(stops[0]);
        }

        var next = at + direction;
        return next >= 0 && next < stops.Count && MoveTo(stops[next]);
    }

    /// <summary>Gives one stop the keyboard, with a box's text ready to be typed over.</summary>
    private static bool MoveTo(Control stop)
    {
        stop.Focus();

        if (stop is TextBox box)
        {
            box.SelectAll();
        }

        return true;
    }

    /// <summary>Everything in one row that can take the keyboard, in the order it is read.</summary>
    /// <remarks>
    /// The buttons as well as the boxes: the folder answer, the preview, approve and withdraw, the
    /// one that uses this dance for every track saying the same thing, the spellings the list
    /// offers and the one that says the value is not a dance. Most of what a row can say, it says
    /// through those. Whichever of a pair is showing, and nothing hidden or shut off, so a walk
    /// never stops on something that is not there.
    /// </remarks>
    private static List<Control> StopsOf(Control row)
    {
        var stops = new List<Control>();

        foreach (var control in row.GetVisualDescendants().OfType<Control>())
        {
            if (!control.Focusable || !control.IsEffectivelyVisible || !control.IsEffectivelyEnabled)
            {
                continue;
            }

            // The control itself, never the parts it is drawn from: a box whose template holds
            // something that can take the keyboard is still one stop.
            if (!stops.Any(stop => stop.IsVisualAncestorOf(control)))
            {
                stops.Add(control);
            }
        }

        return stops;
    }

    /// <summary>The selected row's three inputs, in the order they are read.</summary>
    private List<Control> FieldsOfSelectedRow()
    {
        return ViewModel?.Selected is not { } selected || Queue.ContainerFromItem(selected) is not { } container
            ? []
            :
            [
                .. container.GetVisualDescendants()
                    .OfType<Control>()
                    .Where(control => control.Classes.Contains("field"))
            ];
    }

    private static string TextOf(Control field) =>
        field is TextBox box ? box.Text ?? string.Empty : string.Empty;

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ReviewRowViewModel row } && ViewModel is { } viewModel)
        {
            Handlers.Run(UiStrings.Review_PreviewFailed, () => viewModel.TogglePreviewAsync(row));
        }
    }

    /// <summary>
    /// Closes a row's picker the moment its box is left. The picker overlays the rows beneath, so
    /// one left open under another would paint two lists into the same space.
    /// </summary>
    /// <summary>Enough vertical room for the picker's twelve rows and its border.</summary>
    private const double PickerAllowance = 340;

    private void BringPickerIntoView(ReviewRowViewModel row)
    {
        if (!row.IsPickerOpen || Queue.ContainerFromItem(row) is not { } container)
        {
            return;
        }

        container.BringIntoView(new Rect(
            0, 0, container.Bounds.Width, container.Bounds.Height + PickerAllowance));
    }

    private void OnDanceLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewRowViewModel row })
        {
            row.ClosePicker();
        }
    }

    private void OnSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string suggestion, Tag: ReviewRowViewModel row })
        {
            row.Take(suggestion);
        }
    }

    /// <summary>Seeking by clicking the bar, which is what makes skimming a track possible.</summary>
    private void OnPreviewBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ProgressBar bar || ViewModel is not { PreviewDurationSeconds: > 0 } viewModel)
        {
            return;
        }

        var ratio = Math.Clamp(e.GetPosition(bar).X / bar.Bounds.Width, 0, 1);
        Handlers.Run(
            UiStrings.Review_SeekPreviewFailed,
            () => viewModel.SeekPreviewAsync(TimeSpan.FromSeconds(ratio * viewModel.PreviewDurationSeconds)));
    }
}
