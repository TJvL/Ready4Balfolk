using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI.Platform;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);
        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Main;

}
