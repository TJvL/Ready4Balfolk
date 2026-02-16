using System;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Avalonia;

namespace Ready4Balfolk.UI.Views.Dialogs.Message;

public partial class RequestMessageDialogView : ReactiveWindow<RequestMessageDialogViewModel>
{
    public RequestMessageDialogView()
    {
        InitializeComponent();

        Opened += (_, _) => OkButton.Focus();

        this.WhenActivated(d => this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())
            .DisposeWith(d));
    }
}
