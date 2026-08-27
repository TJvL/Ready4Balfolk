using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

public partial class HistoryToolbarView : ReactiveUserControl<HistoryViewModel>
{
    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    public HistoryToolbarView()
    {
        InitializeComponent();
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiStrings.HistoryToolbar_ExportTitle,
            SuggestedFileName = "queue_history",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
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
