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

    private void OnToggleClick(object? sender, RoutedEventArgs e) => App.Services.GetRequiredService<NavigationService>().IsHistoryMode = false;
}
