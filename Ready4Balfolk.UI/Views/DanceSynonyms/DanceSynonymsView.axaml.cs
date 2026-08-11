using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceSynonyms;

public partial class DanceSynonymsView : ReactiveUserControl<DanceSynonymsViewModel>
{
    private static readonly FilePickerFileType JsonFileType = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"]
    };

    public DanceSynonymsView()
    {
        InitializeComponent();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var confirmationService = App.Services.GetRequiredService<IConfirmationService>();
        if (!await confirmationService.ConfirmAsync(
                UiStrings.DanceSynonyms_ImportTitle,
                UiStrings.DanceSynonyms_ImportConfirmMessage,
                UiStrings.DanceSynonyms_ImportButton,
                UiStrings.DanceSynonyms_CancelButton))
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
            Title = UiStrings.DanceSynonyms_ImportFilePickerTitle,
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            try
            {
                await ViewModel!.ImportAsync(new FileInfo(path));
            }
            catch (InvalidDataException ex)
            {
                App.Services.GetRequiredService<INotificationService>()
                    .Show(ex.Message, NotificationSeverity.Warning);
            }
            catch (Exception ex)
            {
                _ = App.Services.GetRequiredService<ILoggerService>()
                    .ErrorAsync("Failed to import dance synonyms", ex);
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
            Title = UiStrings.DanceSynonyms_ExportFilePickerTitle,
            SuggestedFileName = "dance_synonyms",
            FileTypeChoices = [JsonFileType]
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            try
            {
                await ViewModel!.ExportAsync(new FileInfo(path));
            }
            catch (Exception ex)
            {
                _ = App.Services.GetRequiredService<ILoggerService>()
                    .ErrorAsync("Failed to export dance synonyms", ex);
            }
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().CurrentScreen = Screen.Main;

    private void OnEditTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        textBox.GetObservable(IsVisibleProperty)
            .Where(static visible => visible)
            .Subscribe(_ => Dispatcher.UIThread.Post(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Input));
    }

    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: DanceSynonymEntryViewModel vm })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.ConfirmEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEdit();
            e.Handled = true;
        }
    }

    private void OnSynonymTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        textBox.GetObservable(IsVisibleProperty)
            .Where(static visible => visible)
            .Subscribe(_ => Dispatcher.UIThread.Post(() => textBox.Focus(), DispatcherPriority.Input));
    }

    private void OnNewSynonymKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: DanceSynonymEntryViewModel vm })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.RequestConfirmAddSynonym();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.RequestCancelAddSynonym();
            e.Handled = true;
        }
    }
}
