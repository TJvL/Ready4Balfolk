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

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>
/// The review queue, driven from the keyboard.
/// </summary>
/// <remarks>
/// Two thousand mouse trips is the difference between an evening and never, so a row is answered
/// without the hands leaving the keys: selecting one puts the caret where the typing has to start,
/// Tab walks its three fields, Enter answers the row and Shift+Enter answers the folder.
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
        if (ViewModel is not { Selected: { } selected })
        {
            return;
        }

        // The list of names owns the arrows and Enter while it is open: walking what it found and
        // taking one is the whole point of it being there. The row that owns the focused box, not
        // the selected row: clicking into a box does not move the selection, and the picker that
        // opened belongs to where the typing is.
        var typing = (e.Source as Control)?.DataContext as ReviewRowViewModel ?? selected;
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

        // An if-chain rather than a switch on the key: a switch over an enum invites "populate
        // every case", and this one has 250 of them.
        if (e.Key is Key.Tab)
        {
            // Round this row's own fields rather than out of it: what follows a title is the next
            // thing to type about this track, not the button beside it.
            e.Handled = MoveWithinRow(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
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
            _ = ViewModel.TogglePreviewAsync(selected);
        }
        else if (e.Key is Key.Escape)
        {
            _ = ViewModel.StopPreviewAsync();
        }
        else if (e.Key is Key.Left or Key.Right && ViewModel.IsPreviewing)
        {
            // Only while something is playing, so they stay ordinary editing keys the rest of the
            // time: a typo in the middle of a title still has to be reachable.
            _ = ViewModel.SeekByAsync(TimeSpan.FromSeconds(e.Key is Key.Left ? -5 : 5));
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

    /// <summary>Moves the caret round the row's own fields, wrapping at either end.</summary>
    private bool MoveWithinRow(int direction)
    {
        var fields = FieldsOfSelectedRow();
        if (fields.Count == 0 || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }

        // The focus sits on a text box inside the field rather than on the field itself, so a
        // reference check alone would never find where the caret is.
        var at = fields.FindIndex(field => field == focused || field.IsVisualAncestorOf(focused));
        if (at < 0)
        {
            return false;
        }

        var next = fields[(at + direction + fields.Count) % fields.Count];
        next.Focus();

        if (next is TextBox box)
        {
            box.SelectAll();
        }

        return true;
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

    private async void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ReviewRowViewModel row } && ViewModel is { } viewModel)
        {
            await viewModel.TogglePreviewAsync(row);
        }
    }

    /// <summary>
    /// Closes a row's picker the moment its box is left. The picker overlays the rows beneath, so
    /// one left open under another would paint two lists into the same space.
    /// </summary>
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
        _ = viewModel.SeekPreviewAsync(TimeSpan.FromSeconds(ratio * viewModel.PreviewDurationSeconds));
    }
}
