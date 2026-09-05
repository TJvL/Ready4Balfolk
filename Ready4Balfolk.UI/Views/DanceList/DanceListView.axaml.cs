using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.DanceList;

public partial class DanceListView : ReactiveUserControl<DanceListViewModel>
{
    public DanceListView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The offline path to a newer list: a <c>dances.json</c> carried in on a stick, for a machine
    /// that never reaches the internet. It goes through the same reader a download does.
    /// </summary>
    private void OnUpdateFromFileClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.DanceList_UpdateFromFileFailed, async () =>
        {
            var path = await App.Services.GetRequiredService<IFilePickerService>()
                .PickFileToOpenAsync(UiStrings.DanceList_UpdateFromFileTip, FileKind.Json);

            if (path is not null)
            {
                // Every failure is reported by the view model as a notification, because a refused
                // file is an ordinary answer here rather than an exception the user can act on.
                await ViewModel!.UpdateFromFileAsync(path);
            }
        });
}
