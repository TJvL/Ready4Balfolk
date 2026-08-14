using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public partial class TrackCatalogToolbarView : ReactiveUserControl<TrackCatalogViewModel>
{
    public TrackCatalogToolbarView()
    {
        InitializeComponent();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsDanceListMode = true;
}
