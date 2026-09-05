using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Discovery;

public partial class DiscoveryView : ReactiveUserControl<DiscoveryViewModel>
{
    /// <summary>
    /// Whether the screen carries its own save button for the folders and tags.
    /// </summary>
    /// <remarks>
    /// True in the settings, where nothing else would commit them. False inside the wizard, whose
    /// continue button already means "save this and move on": two buttons that both look like the
    /// way forward is how a step gets left half applied.
    /// </remarks>
    public static readonly StyledProperty<bool> ShowSaveButtonProperty =
        AvaloniaProperty.Register<DiscoveryView, bool>(nameof(ShowSaveButton), defaultValue: true);

    /// <summary>Where a dance the published list does not carry is proposed.</summary>
    private const string DanceListUrl = "https://tjvl.github.io/BigBalfolkList/";

    public DiscoveryView()
    {
        InitializeComponent();
    }

    private void OnDanceListClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run("Failed to open the dance list website", async () =>
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel)
            {
                await topLevel.Launcher.LaunchUriAsync(new Uri(DanceListUrl));
            }
        });

    public bool ShowSaveButton
    {
        get => GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }
}
