using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

public partial class HistoryToolbarView : ReactiveUserControl<HistoryViewModel>
{
    public HistoryToolbarView()
    {
        InitializeComponent();
    }

    private void OnExportClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.HistoryToolbar_ExportFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickWhereToSaveAsync(UiStrings.HistoryToolbar_ExportTitle, "queue_history", FileKind.Json);

            if (path is not null)
            {
                await ViewModel!.ExportAsync(path);
            }
        });

    private void OnExportReportClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.HistoryToolbar_ExportReportFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickWhereToSaveAsync(UiStrings.HistoryToolbar_ExportReportTitle, "queue_history", FileKind.Html);

            if (path is not null)
            {
                await ViewModel!.ExportReportAsync(path);
            }
        });

    private void OnExportSpreadsheetClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.HistoryToolbar_ExportSpreadsheetFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickWhereToSaveAsync(UiStrings.HistoryToolbar_ExportSpreadsheetTitle, "queue_history", FileKind.Csv);

            if (path is not null)
            {
                await ViewModel!.ExportSpreadsheetAsync(path);
            }
        });

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsHistoryMode = false;
}
