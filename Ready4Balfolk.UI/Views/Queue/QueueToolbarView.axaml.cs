using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Controls;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Dialogs.Message;

namespace Ready4Balfolk.UI.Views.Queue;

public partial class QueueToolbarView : ReactiveUserControl<QueueViewModel>
{
    public QueueToolbarView()
    {
        InitializeComponent();
    }

    private async void OnMessageClick(object? sender, RoutedEventArgs e)
    {
        Tooltips.Dismiss(sender);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window owner)
        {
            return;
        }

        var dialogVm = new RequestMessageDialogViewModel();
        var dialog = new RequestMessageDialogView
        {
            DataContext = dialogVm
        };
        await dialog.ShowDialog(owner);

        if (dialogVm.DialogResult == true)
        {
            var duration = dialogVm.UseDelay
                ? TimeSpan.FromSeconds((double)dialogVm.DelaySeconds)
                : (TimeSpan?)null;
            ViewModel?.EnqueueMessage(dialogVm.Message, duration);
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsHistoryMode = true;
}
