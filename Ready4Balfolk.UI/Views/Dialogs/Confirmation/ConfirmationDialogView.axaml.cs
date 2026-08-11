using System;
using System.Reactive.Linq;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Dialogs.Confirmation;

public partial class ConfirmationDialogView : ReactiveWindow<ConfirmationDialogViewModel>
{
    public ConfirmationDialogView()
    {
        InitializeComponent();

        Opened += (_, _) => ConfirmButton.Focus();

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())));
    }
}
