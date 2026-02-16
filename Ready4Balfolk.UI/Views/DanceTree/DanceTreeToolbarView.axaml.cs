using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceTree;

public partial class DanceTreeToolbarView : ReactiveUserControl<DanceTreeViewModel>
{
    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    public DanceTreeToolbarView()
    {
        InitializeComponent();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var confirmationService = App.Services.GetRequiredService<IConfirmationService>();
        if (!await confirmationService.ConfirmAsync(
                UiStrings.DanceTreeToolbar_ImportTitle,
                UiStrings.DanceTreeToolbar_ImportConfirmMessage,
                UiStrings.DanceTreeToolbar_ImportButton,
                UiStrings.DanceTreeToolbar_CancelButton))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiStrings.DanceTreeToolbar_ImportTitle,
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await ViewModel!.ImportAsync(new FileInfo(path));
        }
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
            Title = UiStrings.DanceTreeToolbar_ExportTitle,
            SuggestedFileName = "dance_tree",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await ViewModel!.ExportAsync(new FileInfo(path));
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsTreeViewMode = false;
}
