using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI.Views.Dialogs.QrCode;

public partial class QrCodeDialogView : ReactiveWindow<QrCodeDialogViewModel>
{
    public QrCodeDialogView()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the surface
        // is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);

        // Something in the window holds the keyboard from the moment it opens, the way every other
        // dialog here does. There is one thing to press in this one, and it is the one that closes
        // it.
        Opened += (_, _) => CloseButton.Focus();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
