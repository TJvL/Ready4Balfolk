using System.IO.Abstractions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

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
        var path = await App.Services.GetRequiredService<IFilePickerService>()
            .PickFileToOpenAsync(UiStrings.Wizard_DanceList_Import, FileKind.Json);

        if (path is not null)
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
