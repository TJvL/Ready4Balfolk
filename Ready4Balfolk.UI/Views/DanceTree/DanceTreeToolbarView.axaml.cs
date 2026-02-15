using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
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
                "Import Dance Tree",
                "Importing will permanently overwrite the current dance tree. Consider exporting a backup first.\n\nContinue with import?",
                "Import",
                "Cancel"))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Dance Tree",
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            await ViewModel!.ImportAsync(new FileInfo(path));
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Dance Tree",
            SuggestedFileName = "dance_tree",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
            await ViewModel!.ExportAsync(new FileInfo(path));
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsTreeViewMode = false;
}
