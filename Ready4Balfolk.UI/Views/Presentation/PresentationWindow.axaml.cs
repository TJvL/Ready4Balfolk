using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.UI.Views.Presentation;

public partial class PresentationWindow : Window
{
    private bool _isBorderless;

    public int WindowIndex { get; set; }

    public bool AllowClose { get; set; }

    public bool IsBorderless
    {
        get => _isBorderless;
        set
        {
            _isBorderless = value;
            ApplyBorderlessState();
        }
    }

    public PresentationWindow()
    {
        InitializeComponent();

        // Before the window is shown, so the compositor already knows the app id when the
        // surface is mapped. See WaylandAppId.
        WaylandAppId.Apply(this);
        DataContext = App.Services.GetRequiredService<PresentationDisplayViewModel>();
        DoubleTapped += (_, _) =>
        {
            _isBorderless = !_isBorderless;
            ApplyBorderlessState();
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private void ApplyBorderlessState()
    {
        if (_isBorderless)
        {
            WindowDecorations = WindowDecorations.None;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowDecorations = WindowDecorations.Full;
            WindowState = WindowState.Normal;
        }
    }
}
