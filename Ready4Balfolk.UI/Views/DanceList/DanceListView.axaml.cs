using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceList;

public partial class DanceListView : ReactiveUserControl<DanceListViewModel>
{
    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    public DanceListView()
    {
        InitializeComponent();
    }

    private void OnCategoryNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel!.RenameCategoryCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    private void OnNewDanceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel!.AddDanceCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    private void OnNewNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel!.AddNameCommand.Execute().Subscribe();
            e.Handled = true;
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var confirmations = App.Services.GetRequiredService<IConfirmationService>();
        if (!await confirmations.ConfirmAsync(
                UiStrings.DanceList_ImportTitle,
                UiStrings.DanceList_ImportConfirmMessage,
                UiStrings.DanceList_Import,
                UiStrings.DanceList_DeleteCancel))
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
            Title = UiStrings.DanceList_ImportTitle,
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            try
            {
                await ViewModel!.ImportAsync(new FileInfo(path));
            }
            catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException)
            {
                App.Services.GetRequiredService<INotificationService>()
                    .Show(exception.Message, NotificationSeverity.Warning);
            }
            catch (IOException exception)
            {
                _ = App.Services.GetRequiredService<ILoggerService>()
                    .ErrorAsync("Failed to import the dance list", exception);
            }
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
            Title = UiStrings.DanceList_Export,
            SuggestedFileName = "dance_list",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            try
            {
                await ViewModel!.ExportAsync(new FileInfo(path));
            }
            catch (IOException exception)
            {
                _ = App.Services.GetRequiredService<ILoggerService>()
                    .ErrorAsync("Failed to export the dance list", exception);
            }
        }
    }
}
