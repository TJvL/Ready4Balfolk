using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Controls;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Queue;

public partial class QueueToolbarView : ReactiveUserControl<QueueViewModel>
{
    public QueueToolbarView()
    {
        InitializeComponent();
    }

    private void OnMessageClick(object? sender, RoutedEventArgs e)
    {
        Tooltips.Dismiss(sender);

        Handlers.Run(
            "Failed to add the message", UiStrings.QueueToolbar_AddMessageFailed, async () =>
            {
                if (await App.Services.GetRequiredService<IDialogService>().RequestMessageAsync() is { } request)
                {
                    ViewModel?.EnqueueMessage(request.Message, request.Duration);
                }
            });
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsHistoryMode = true;
}
