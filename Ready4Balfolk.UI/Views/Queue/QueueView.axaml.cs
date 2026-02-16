using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Avalonia;
using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.UI.Views.Queue;

public partial class QueueView : ReactiveUserControl<QueueViewModel>
{
    private static readonly DataFormat<string> QueueDragFormat =
        DataFormat.CreateStringApplicationFormat("QueueDragIndex");

    private Point? _dragStartPoint;
    private int _dragStartIndex = -1;
    private bool _dropAbove;

    public QueueView()
    {
        InitializeComponent();

        QueueListBox.AddHandler(DragDrop.DropEvent, OnDrop);
        QueueListBox.AddHandler(DragDrop.DragOverEvent, OnDragOver);

        // Register with handledEventsToo so drag works even after ListBox
        // consumes pointer events for its own selection handling.
        QueueListBox.AddHandler(PointerPressedEvent, OnQueuePointerPressed, handledEventsToo: true);
        QueueListBox.AddHandler(PointerMovedEvent, OnQueuePointerMoved, handledEventsToo: true);
        QueueListBox.AddHandler(PointerReleasedEvent, OnQueuePointerReleased, handledEventsToo: true);

        QueueListBox.ContainerPrepared += OnContainerPrepared;
        QueueListBox.ContainerClearing += OnContainerClearing;
        QueueListBox.SelectionChanged += OnSelectionChanged;
    }

    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem item && item.DataContext is AutoTrackQueueItem)
        {
            item.Classes.Add("autoTrack");
        }
    }

    private static void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container is ListBoxItem item)
        {
            item.Classes.Remove("autoTrack");
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (QueueListBox.SelectedItem is AutoTrackQueueItem)
        {
            QueueListBox.SelectedItem = null;
        }
    }

    private void OnQueuePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(QueueListBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var listBoxItem = FindParent<ListBoxItem>(e.Source as Control);
        if (listBoxItem?.DataContext is AutoTrackQueueItem or null)
        {
            return;
        }

        _dragStartIndex = QueueListBox.IndexFromContainer(listBoxItem);
        _dragStartPoint = e.GetPosition(QueueListBox);
    }

    private async void OnQueuePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartPoint == null || _dragStartIndex < 0)
        {
            return;
        }

        var currentPoint = e.GetPosition(QueueListBox);
        var diff = currentPoint - _dragStartPoint.Value;

        if (Math.Abs(diff.Y) < 8)
        {
            return;
        }

        var index = _dragStartIndex;
        _dragStartPoint = null;
        _dragStartIndex = -1;

        var item = DataTransferItem.Create(QueueDragFormat, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        HideDropIndicator();
    }

    private void OnQueuePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPoint = null;
        _dragStartIndex = -1;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(QueueDragFormat))
        {
            e.DragEffects = DragDropEffects.None;
            HideDropIndicator();
            return;
        }

        e.DragEffects = DragDropEffects.Move;

        var target = FindParent<ListBoxItem>(e.Source as Control);
        if (target == null)
        {
            HideDropIndicator();
            return;
        }

        var pos = e.GetPosition(target);
        _dropAbove = pos.Y < target.Bounds.Height / 2;

        var edgeY = _dropAbove ? 0 : target.Bounds.Height;
        var point = target.TranslatePoint(new Point(0, edgeY), QueueListBox);
        if (point is null)
        {
            HideDropIndicator();
            return;
        }

        Canvas.SetLeft(DropIndicatorLine, 0);
        Canvas.SetTop(DropIndicatorLine, point.Value.Y - (DropIndicatorLine.Height / 2));
        DropIndicatorLine.Width = QueueListBox.Bounds.Width;
        DropIndicatorLine.IsVisible = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        HideDropIndicator();

        var indexStr = e.DataTransfer.TryGetValue(QueueDragFormat);
        if (indexStr is null || e.Source is not Control source)
        {
            return;
        }

        var oldIndex = int.Parse(indexStr, System.Globalization.CultureInfo.InvariantCulture);

        var targetItem = FindParent<ListBoxItem>(source);
        if (targetItem == null)
        {
            return;
        }

        var newIndex = QueueListBox.IndexFromContainer(targetItem);
        if (newIndex >= 0)
        {
            ViewModel?.MoveItem(oldIndex, newIndex);
        }
    }

#pragma warning disable CA1822 // Avalonia source-generator field not visible to analyzer
    private void HideDropIndicator() => DropIndicatorLine.IsVisible = false;
#pragma warning restore CA1822

    private void OnQueueKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            ViewModel?.DeleteSelectedItem();
            e.Handled = true;
        }
    }

    private void OnRefreshAutoQueuedClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.RefreshAutoTrack();

    private void OnPinAutoQueuedClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AutoTrackQueueItem item })
        {
            ViewModel?.PinAutoTrack(item);
        }
    }

    private static T? FindParent<T>(Control? control) where T : Control
    {
        while (control != null)
        {
            if (control is T found)
            {
                return found;
            }

            control = control.Parent as Control;
        }

        return null;
    }
}
