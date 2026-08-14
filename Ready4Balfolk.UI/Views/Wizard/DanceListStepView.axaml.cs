using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;

namespace Ready4Balfolk.UI.Views.Wizard;

public partial class DanceListStepView : ReactiveUserControl<DanceListStepViewModel>
{
    public DanceListStepView()
    {
        InitializeComponent();
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
