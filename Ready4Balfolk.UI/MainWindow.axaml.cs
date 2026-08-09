using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Main;

    // Back to where it was opened from, which is settings: discovery is reached through it and
    // dropping the user on the main screen loses their place.
    private void OnBackToSettingsClick(object? sender, RoutedEventArgs e) =>
        App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Settings;
}
