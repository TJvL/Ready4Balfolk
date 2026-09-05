using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Controls;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Dialogs.QrCode;

namespace Ready4Balfolk.UI.Views.Toolbar;

public partial class ToolbarView : ReactiveUserControl<ToolbarViewModel>
{
    public ToolbarView()
    {
        InitializeComponent();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this) as Window)?.Close();

    private void OnHelpClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Help;

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Settings;

    private void OnReviewClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Review;

    private void OnDisplayAddressClick(object? sender, RoutedEventArgs e)
    {
        Tooltips.Dismiss(sender);
        Handlers.Run("Failed to show the display address", () => ShowAddressAsync(ViewModel?.DisplayAddress()));
    }

    private void OnRemoteAddressClick(object? sender, RoutedEventArgs e)
    {
        Tooltips.Dismiss(sender);
        Handlers.Run("Failed to show the remote address", () => ShowAddressAsync(ViewModel?.RemoteAddress()));
    }

    /// <summary>Puts the address on screen as something a phone can be pointed at.</summary>
    /// <remarks>
    /// Nothing at all when the server has no address to give, which is a server that is starting or
    /// one that failed to bind: an empty code is worse than no code.
    /// </remarks>
    private async Task ShowAddressAsync(QrCodeDialogViewModel? address)
    {
        if (address is null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new QrCodeDialogView { DataContext = address };
        await dialog.ShowDialog(owner);
    }
}
