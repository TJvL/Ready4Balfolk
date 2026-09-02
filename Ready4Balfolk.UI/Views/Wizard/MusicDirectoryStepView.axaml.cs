using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Wizard;

public partial class MusicDirectoryStepView : ReactiveUserControl<MusicDirectoryStepViewModel>
{
    public MusicDirectoryStepView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var path = await App.Services.GetRequiredService<IFilePickerService>()
            .PickFolderAsync(UiStrings.Settings_SelectMusicDirectory);

        if (path is not null)
        {
            ViewModel!.MusicDirectoryPath = path;
        }
    }
}
