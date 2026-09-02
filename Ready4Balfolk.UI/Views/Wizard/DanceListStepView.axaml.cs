using System.IO.Abstractions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

public partial class DanceListStepView : ReactiveUserControl<DanceListStepViewModel>
{
    public DanceListStepView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The offline way in: a <c>dances.json</c> carried on a stick, for a machine that will never
    /// reach BigBalfolkList. The same reader takes it as a download would.
    /// </summary>
    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiStrings.Wizard_DanceList_Import,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("dances.json") { Patterns = ["*.json"] }]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await ViewModel!.ImportAsync(new FileSystem().FileInfo.New(path));
        }
    }

    private async void OnSourceLinkClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            await topLevel.Launcher.LaunchUriAsync(ViewModel!.SourceUri);
        }
    }
}
