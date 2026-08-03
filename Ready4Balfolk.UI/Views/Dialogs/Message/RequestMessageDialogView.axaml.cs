using System;
using System.Reactive.Linq;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;

namespace Ready4Balfolk.UI.Views.Dialogs.Message;

public partial class RequestMessageDialogView : ReactiveWindow<RequestMessageDialogViewModel>
{
    public RequestMessageDialogView()
    {
        InitializeComponent();

        Opened += (_, _) => OkButton.Focus();

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())));
    }
}
