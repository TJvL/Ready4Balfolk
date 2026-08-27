using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;

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

    /// <summary>
    /// The offline path to a newer list: a <c>dances.json</c> carried in on a stick, for a machine
    /// that never reaches the internet. It goes through the same reader a download does.
    /// </summary>
    private async void OnUpdateFromFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiStrings.DanceList_UpdateFromFileTip,
            AllowMultiple = false,
            FileTypeFilter = [JsonFileType]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            // Every failure is reported by the view model as a notification, because a refused file
            // is an ordinary answer here rather than an exception the user can act on.
            await ViewModel!.UpdateFromFileAsync(path);
        }
    }
}
