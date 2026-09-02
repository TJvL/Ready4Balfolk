using System;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

public partial class HistoryToolbarView : ReactiveUserControl<HistoryViewModel>
{
    public HistoryToolbarView()
    {
        InitializeComponent();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var path = await App.Services.GetRequiredService<IFilePickerService>()
            .PickWhereToSaveAsync(UiStrings.HistoryToolbar_ExportTitle, "queue_history", FileKind.Json);

        if (path is not null)
        {
            try
            {
                await ViewModel!.ExportAsync(path);
            }
            catch (Exception ex)
            {
                _ = App.Services.GetRequiredService<ILoggerService>()
                    .ErrorAsync("Failed to export queue history", ex);
            }
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsHistoryMode = false;
}
