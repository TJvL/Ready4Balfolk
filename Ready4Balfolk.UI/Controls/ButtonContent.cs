using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Controls;

/// <summary>
/// Content for a button that shows either its icon or a text label, so every button in the
/// application can be switched between the two from a single setting.
/// </summary>
/// <remarks>
/// ShowText is set for every instance by a style in App.axaml bound to the ShowButtonText dynamic
/// resource, which App keeps in step with the setting. Individual call sites only supply the icon
/// and the label.
/// </remarks>
public class ButtonContent : ContentControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ButtonContent, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ButtonContent, string?>(nameof(Text));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ButtonContent, double>(nameof(IconSize), defaultValue: 16);

    public static readonly StyledProperty<bool> ShowTextProperty =
        AvaloniaProperty.Register<ButtonContent, bool>(nameof(ShowText));

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool ShowText
    {
        get => GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    static ButtonContent()
    {
        IconProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
        TextProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
        IconSizeProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
        ShowTextProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
    }

    public ButtonContent()
    {
        UpdateContent();
    }

    private void UpdateContent()
    {
        // Falls back to the icon when a label is missing, so a call site that has not been given
        // one yet shows the icon rather than an empty button.
        Content = ShowText && !string.IsNullOrWhiteSpace(Text)
            ? new TextBlock
            {
                Text = Text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
            : new PathIcon
            {
                Data = Icon,
                Width = IconSize,
                Height = IconSize
            };
    }
}
