using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI.Avalonia.Reactive;
using Ready4Balfolk.UI.Resources;
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

    /// <summary>
    /// Whether Enter in the draft-pattern box declares the draft, the way the button beside it does.
    /// </summary>
    /// <remarks>
    /// True where this box is the only thing Enter could mean. False inside the wizard, whose host
    /// window has its own default button on Enter (Continue) that this box must not steal: swallowing
    /// the keystroke here would silently declare a half-typed rule instead of advancing the step.
    /// </remarks>
    public static readonly StyledProperty<bool> DeclareOnEnterProperty =
        AvaloniaProperty.Register<DiscoveryView, bool>(nameof(DeclareOnEnter), defaultValue: true);

    /// <summary>Where a dance the published list does not carry is proposed.</summary>
    private const string DanceListUrl = "https://tjvl.github.io/BigBalfolkList/";

    public DiscoveryView()
    {
        InitializeComponent();
    }

    /// <summary>Enter declares the draft, the way the button beside it does, unless this host says not to.</summary>
    private void OnDraftPatternKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter || e.KeyModifiers is not KeyModifiers.None || !DeclareOnEnter)
        {
            return;
        }

        ViewModel?.DeclareDraftCommand.Execute().Subscribe();
        e.Handled = true;
    }

    private void OnDanceListClick(object? sender, RoutedEventArgs e) =>
        Handlers.Run(UiStrings.DanceList_OpenSiteFailed, async () =>
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

    public bool DeclareOnEnter
    {
        get => GetValue(DeclareOnEnterProperty);
        set => SetValue(DeclareOnEnterProperty, value);
    }
}
