using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Controls;

/// <summary>
/// Content for a button that shows either its icon or a text label, so every button in the
/// application can be switched between the two from a single setting.
/// </summary>
/// <remarks>
/// <para>
/// ShowText is set for every instance by a style in App.axaml bound to the ShowButtonText dynamic
/// resource, which App keeps in step with the setting. Individual call sites only supply the icon
/// and the label.
/// </para>
/// <para>
/// The label is also the button's accessible name. With the setting off a button draws nothing but
/// a PathIcon, and a path has no text in it, so a screen reader reading the toolbar would find a
/// row of buttons with nothing to call any of them. The name follows the label rather than the
/// setting: what the button is called does not change because the DJ chose icons.
/// </para>
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
        TextProperty.Changed.AddClassHandler<ButtonContent>((c, _) =>
        {
            c.UpdateContent();
            c.NameTheButton();
        });
        IconSizeProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
        ShowTextProperty.Changed.AddClassHandler<ButtonContent>((c, _) => c.UpdateContent());
    }

    public ButtonContent()
    {
        UpdateContent();
    }

    /// <summary>Names the button once it knows which button it is in.</summary>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        NameTheButton();
    }

    /// <summary>
    /// Gives the button this is the content of the label as its accessible name.
    /// </summary>
    /// <remarks>
    /// The button rather than this, because the button is what a screen reader lands on and reads.
    /// The search stops at a panel, so this only ever names a button whose whole content it is: a
    /// button holding two of these, one per state, or one of these beside a badge, has something
    /// else to say about itself and says it in its own XAML. Letting whichever was written last
    /// name the button would call Pause "Play" half the evening. An explicit name is never
    /// overwritten for the same reason.
    /// </remarks>
    private void NameTheButton()
    {
        if (Text is not { } label || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        for (var element = Parent; element is not null; element = element.Parent)
        {
            if (element is Panel)
            {
                return;
            }

            if (element is Button button)
            {
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
                {
                    AutomationProperties.SetName(button, label);
                }

                return;
            }
        }
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
