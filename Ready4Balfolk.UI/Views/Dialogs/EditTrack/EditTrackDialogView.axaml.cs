using System;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Dialogs.EditTrack;

public partial class EditTrackDialogView : ReactiveWindow<EditTrackDialogViewModel>
{
    public EditTrackDialogView()
    {
        InitializeComponent();

        Opened += (_, _) => DanceBox.Focus();

        // The picker is walked with the arrows, exactly as on a review row: Down and Up move the
        // highlight, Enter takes it, Escape closes the picker before it closes the dialog.
        DanceBox.AddHandler(KeyDownEvent, OnDanceKeyDown, handledEventsToo: false);

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(result => result.HasValue)
            .Subscribe(_ => Close())));
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
