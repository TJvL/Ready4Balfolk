using System;
using System.Reactive.Linq;
using ReactiveUI.Avalonia.Reactive;
using ReactiveUI.Reactive;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI.Views.Dialogs.Message;

public partial class RequestMessageDialogView : ReactiveWindow<RequestMessageDialogViewModel>
{
    public RequestMessageDialogView()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);

        // The box, not the button: the DJ opened this to type an announcement, and a caret waiting
        // on OK costs a click before every one of them. OK stays the default, so return still sends.
        Opened += (_, _) => MessageBox.Focus();

        this.WhenActivated(d => d(this.WhenAnyValue(x => x.ViewModel!.DialogResult)
            .Where(r => r.HasValue)
            .Subscribe(_ => Close())));
    }
}
