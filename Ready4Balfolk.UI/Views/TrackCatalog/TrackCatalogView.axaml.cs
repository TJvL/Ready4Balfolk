using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI.Avalonia.Reactive;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public partial class TrackCatalogView : ReactiveUserControl<TrackCatalogViewModel>
{
    private string? _lastSortColumn;
    private int _clickCount;

    public TrackCatalogView()
    {
        InitializeComponent();

        // Tunnelled, the way the queue takes its own keys: the grid reads Enter as a step down
        // the rows, and here it is the answer to "this one", which is the whole reason a person
        // walked the list with the arrows in the first place.
        TracksDataGrid.AddHandler(KeyDownEvent, OnTracksKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Enter puts the highlighted track in the queue, the way a double click does.</summary>
    private void OnTracksKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter
            || e.KeyModifiers is not KeyModifiers.None
            || TracksDataGrid.SelectedItem is not TrackViewModel track)
        {
            return;
        }

        ViewModel?.EnqueueTrackCommand.Execute(track).Subscribe();
        e.Handled = true;
    }

    private void DataGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        // The sort member rather than the header's text: the header is a control carrying an
        // automation id for the scenarios, and a control's ToString is its type name, the same for
        // every column, which would make every second click look like the first click on a new one.
        var columnKey = e.Column.SortMemberPath;

        if (columnKey != _lastSortColumn)
        {
            _lastSortColumn = columnKey;
            _clickCount = 1;
            return; // Let default sorting happen (ascending)
        }

        _clickCount++;

        // Third click: clear sorting
        if (_clickCount >= 3)
        {
            e.Handled = true;
            _clickCount = 0;
            _lastSortColumn = null;
            dataGrid.CollectionView?.SortDescriptions.Clear();
        }
        // Second click: let default sorting happen (descending)
    }

    private void DataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control control && control.FindAncestorOfType<DataGridColumnHeader>() != null)
        {
            return;
        }

        if (TracksDataGrid.SelectedItem is TrackViewModel track)
        {
            ViewModel?.EnqueueTrackCommand.Execute(track).Subscribe();
        }
    }
}
