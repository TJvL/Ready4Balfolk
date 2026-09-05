using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Wizard;

public partial class WelcomeStepView : ReactiveUserControl<WelcomeStepViewModel>
{
    public WelcomeStepView()
    {
        InitializeComponent();
    }

    private void OnSourceLinkClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run("Failed to open the dance list website", async () =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not null)
            {
                await topLevel.Launcher.LaunchUriAsync(new Uri(WelcomeStepViewModel.DanceListSourceUrl));
            }
        });
}
