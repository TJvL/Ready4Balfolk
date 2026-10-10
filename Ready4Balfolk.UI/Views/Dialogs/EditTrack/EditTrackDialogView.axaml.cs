using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Platform;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Dialogs.EditTrack;

public partial class EditTrackDialogView : ReactiveWindow<EditTrackDialogViewModel>
{
    public EditTrackDialogView()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);

        Opened += (_, _) => DanceBox.Focus();

        // The picker is walked with the arrows, exactly as on a review row: Down and Up move the
        // highlight, Enter takes it, Escape closes the picker before it closes the dialog.
        DanceBox.AddHandler(KeyDownEvent, OnDanceKeyDown, handledEventsToo: false);

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(result => result.HasValue)
            .Subscribe(_ => Close())));
    }

    /// <summary>Puts the curve on the sliders on the clipboard, for pasting onto another track.</summary>
    private void OnCopyEqualizer(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Clipboard is not { } clipboard)
        {
            return;
        }

        var json = vm.EqualizerJson;
        Handlers.Run(
            "Failed to copy a track's equalizer to the clipboard",
            UiStrings.EditTrack_ClipboardFailed,
            () => clipboard.SetTextAsync(json));
    }

    /// <summary>Takes a curve off the clipboard; the view model says so when it is not one.</summary>
    private void OnPasteEqualizer(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Clipboard is not { } clipboard)
        {
            return;
        }

        Handlers.Run(
            "Failed to paste a track's equalizer from the clipboard",
            UiStrings.EditTrack_ClipboardFailed,
            async () =>
            {
                var text = await clipboard.TryGetTextAsync();
                vm.PasteEqualizer(text);
            });
    }

    /// <summary>Keeps the walked-to match visible: a highlight below the fold is no choice at all.</summary>
    private void BringHighlightIntoView(EditTrackDialogViewModel vm)
    {
        for (var i = 0; i < vm.DanceMatches.Count; i++)
        {
            if (vm.DanceMatches[i].IsHighlighted)
            {
                MatchList.ContainerFromIndex(i)?.BringIntoView();
                return;
            }
        }
    }

    private void OnDanceKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm || !vm.IsPickerOpen)
        {
            return;
        }

        if (e.Key is Key.Down)
        {
            vm.MoveHighlight(1);
            BringHighlightIntoView(vm);
            e.Handled = true;
        }
        else if (e.Key is Key.Up)
        {
            vm.MoveHighlight(-1);
            BringHighlightIntoView(vm);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter)
        {
            e.Handled = vm.TakeHighlighted();
        }
        else if (e.Key is Key.Escape)
        {
            vm.ClosePicker();
            e.Handled = true;
        }
    }
}
