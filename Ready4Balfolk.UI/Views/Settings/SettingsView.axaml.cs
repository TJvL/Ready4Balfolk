using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.Domain;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    private static readonly FilePickerFileType LogFileType = new("Text files")
    {
        Patterns = ["*.txt"],
        MimeTypes = ["text/plain"]
    };

    /// <summary>Exactly what the player can open, which is decided by BASS and its plugins.</summary>
    private static FilePickerFileType AudioFileType => new(UiStrings.Settings_EndOfNightAudioFiles)
    {
        Patterns = SupportedAudioFormats.Extensions.Count > 0
            ? [.. SupportedAudioFormats.Extensions.Select(extension => $"*{extension}")]
            : ["*"]
    };

    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnRunSetupClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<ApplicationStartup>().ShowSetup();

    /// <summary>
    /// Points the setting at a file the user already has. Nothing is imported or copied: the path
    /// is the whole of the setting.
    /// </summary>
    private async void OnBrowseEndOfNightClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiStrings.Settings_EndOfNightPickerTitle,
            AllowMultiple = false,
            FileTypeFilter = [AudioFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            ViewModel!.EndOfNightAudioPath = path;
        }
    }


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
            await ViewModel!.ExportLogAsync(path);
        }
    }
}
