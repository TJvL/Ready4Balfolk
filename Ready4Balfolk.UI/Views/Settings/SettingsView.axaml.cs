using System;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Settings;

public partial class SettingsView : ReactiveUserControl<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnRunSetupClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<ApplicationStartup>().ShowSetup();

    /// <summary>
    /// Points the setting at a file the user already has. Nothing is imported or copied: the path
    /// is the whole of the setting.
    /// </summary>
    private void OnBrowseEndOfNightClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.Settings_EndOfNightChooseFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickFileToOpenAsync(UiStrings.Settings_EndOfNightPickerTitle, FileKind.Audio);

            if (path is not null)
            {
                ViewModel!.EndOfNightAudioPath = path;
            }
        });

    /// <summary>
    /// The log, where the person asked for it. A place that cannot be written is the ordinary
    /// answer here, not an exceptional one: a stick that was pulled, a folder that is read-only.
    /// </summary>
    private void OnExportLogClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.Settings_ExportLogFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickWhereToSaveAsync(
                    UiStrings.Settings_ExportLogTitle,
                    $"ready4balfolk-log-{DateTime.Now:yyyy-MM-dd-HHmmss}",
                    FileKind.Text);

            if (path is not null)
            {
                await ViewModel!.ExportLogAsync(path);
            }
        });
}
