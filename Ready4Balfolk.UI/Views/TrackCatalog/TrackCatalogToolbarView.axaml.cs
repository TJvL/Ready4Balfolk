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

    /// <summary>Puts the caret in the search box, over whatever is already in it.</summary>
    /// <remarks>
    /// Selected rather than appended to: the shortcut is pressed to look for something else, and
    /// a box that has to be cleared first is half a shortcut.
    /// </remarks>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsDanceListMode = true;
}
