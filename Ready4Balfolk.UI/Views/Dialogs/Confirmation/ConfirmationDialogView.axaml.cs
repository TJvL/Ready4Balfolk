using System;
using System.Reactive.Linq;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI.Views.Dialogs.Confirmation;

public partial class ConfirmationDialogView : ReactiveWindow<ConfirmationDialogViewModel>
{
    public ConfirmationDialogView()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);

        // Focus follows the default button, so return and space agree with each other. Without it
        // a destructive dialog still hands the keyboard to the button that throws things away.
        Opened += (_, _) =>
        {
            var safe = (DataContext as ConfirmationDialogViewModel)?.ConfirmIsSafe ?? false;
            (safe ? ConfirmButton : CancelButton).Focus();
        };

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())));
    }
}
