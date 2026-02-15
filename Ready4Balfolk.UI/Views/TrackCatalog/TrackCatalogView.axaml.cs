using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI.Avalonia;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public partial class TrackCatalogView : ReactiveUserControl<TrackCatalogViewModel>
{
    private string? _lastSortColumn;
    private int _clickCount;

    public TrackCatalogView()
    {
        InitializeComponent();
    }

    private void DataGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
            return;

        var columnHeader = e.Column.Header?.ToString();

        if (columnHeader != _lastSortColumn)
        {
            _lastSortColumn = columnHeader;
            _clickCount = 1;
            return; // Let default sorting happen (ascending)
        }

        _clickCount++;

        // Third click — clear sorting
        if (_clickCount >= 3)
        {
            e.Handled = true;
            _clickCount = 0;
            _lastSortColumn = null;
            dataGrid.CollectionView?.SortDescriptions.Clear();
        }
        // Second click — let default sorting happen (descending)
    }

    private void DataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control control && control.FindAncestorOfType<DataGridColumnHeader>() != null)
            return;

        if (TracksDataGrid.SelectedItem is TrackViewModel track)
            ViewModel?.EnqueueTrackCommand.Execute(track).Subscribe();
    }
}
