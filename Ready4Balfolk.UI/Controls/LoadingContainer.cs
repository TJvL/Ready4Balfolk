using Avalonia;
using Avalonia.Controls;

namespace Ready4Balfolk.UI.Controls;

public class LoadingContainer : ContentControl
{
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<LoadingContainer, bool>(nameof(IsLoading), defaultValue: true);

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    static LoadingContainer()
    {
        IsLoadingProperty.Changed.AddClassHandler<LoadingContainer>((container, _) =>
            container.UpdatePseudoClasses());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses() => PseudoClasses.Set(":loading", IsLoading);
}
