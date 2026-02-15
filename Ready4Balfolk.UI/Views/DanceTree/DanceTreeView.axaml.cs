using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI.Avalonia;

namespace Ready4Balfolk.UI.Views.DanceTree;

public partial class DanceTreeView : ReactiveUserControl<DanceTreeViewModel>
{
    static DanceTreeView()
    {
        // Sync TreeViewItem.IsExpanded → DanceCategoryNode.IsExpanded on every change
        TreeViewItem.IsExpandedProperty.Changed.AddClassHandler<TreeViewItem>((tvi, _) =>
        {
            if (tvi.DataContext is DanceCategoryNode node)
                node.IsExpanded = tvi.IsExpanded;
        });
    }

    public DanceTreeView()
    {
        InitializeComponent();
        // TreeViewItem subscribes to DoubleTapped on PART_HeaderPresenter via CLR event,
        // toggles IsExpanded, and sets e.Handled = true. We listen with handledEventsToo
        // to revert the toggle and trigger random pick instead.
        DanceTreeControl.AddHandler(DoubleTappedEvent, OnTreeDoubleTapped,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        var source = e.Source as Control;
        while (source is not null and not TreeViewItem)
            source = source.Parent as Control;

        if (source is not TreeViewItem { DataContext: DanceCategoryNode or DanceItem } tvi)
            return;

        // Revert the expand/collapse that TreeViewItem just performed
        if (tvi.DataContext is DanceCategoryNode)
            tvi.SetCurrentValue(TreeViewItem.IsExpandedProperty, !tvi.IsExpanded);

        ViewModel?.QuickRandomPick(tvi.DataContext!);
    }

    private void OnEditTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        textBox.GetObservable(IsVisibleProperty)
            .Where(static visible => visible)
            .Subscribe(_ => Dispatcher.UIThread.Post(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Input));
    }

    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;

        var dataContext = (sender as Control)?.DataContext;

        if (e.Key is Key.Enter)
        {
            switch (dataContext)
            {
                case DanceCategoryNode node:
                    node.ConfirmEdit();
                    break;
                case DanceItem item:
                    item.ConfirmEdit();
                    break;
                default:
                    break;
            }
        }
        else // Escape
        {
            switch (dataContext)
            {
                case DanceCategoryNode node:
                    node.CancelEdit();
                    break;
                case DanceItem item:
                    item.CancelEdit();
                    break;
                default:
                    break;
            }
        }

        e.Handled = true;
    }
}
