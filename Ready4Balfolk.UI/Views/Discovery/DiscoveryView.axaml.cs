using Avalonia;
using ReactiveUI.Avalonia.Reactive;

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

    public DiscoveryView()
    {
        InitializeComponent();
    }

    public bool ShowSaveButton
    {
        get => GetValue(ShowSaveButtonProperty);
        set => SetValue(ShowSaveButtonProperty, value);
    }
}
