using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Services;

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
}
