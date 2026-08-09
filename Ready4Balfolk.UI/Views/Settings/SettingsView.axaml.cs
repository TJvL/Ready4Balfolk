using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    private static readonly FilePickerFileType LogFileType = new("Text files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnRunSetupClick(object? sender, RoutedEventArgs e) => App.ShowSetup();

    private async void OnExportLogClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiStrings.Settings_ExportLogTitle,
            SuggestedFileName = $"ready4balfolk-log-{DateTime.Now:yyyy-MM-dd-HHmmss}",
            FileTypeChoices = [LogFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await ViewModel!.ExportLogAsync(new FileInfo(path));
        }
    }
}
