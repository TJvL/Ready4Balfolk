using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceList;

public partial class DanceListToolbarView : ReactiveUserControl<DanceListViewModel>
{
    public DanceListToolbarView()
    {
        InitializeComponent();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) =>
        App.Services.GetRequiredService<NavigationService>().IsDanceListMode = false;
}
