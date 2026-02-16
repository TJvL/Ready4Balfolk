using System;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ready4Balfolk.UI.Views.Dialogs.Confirmation;

public partial class ConfirmationDialogView : ReactiveWindow<ConfirmationDialogViewModel>
{
    public ConfirmationDialogView()
    {
        InitializeComponent();

        Opened += (_, _) => ConfirmButton.Focus();

        this.WhenActivated(d => this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())
            .DisposeWith(d));
    }
}
