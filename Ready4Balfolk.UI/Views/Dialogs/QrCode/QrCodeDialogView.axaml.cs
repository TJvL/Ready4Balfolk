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
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
