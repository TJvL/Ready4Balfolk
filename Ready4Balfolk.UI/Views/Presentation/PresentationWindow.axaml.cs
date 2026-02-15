using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

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
            SystemDecorations = SystemDecorations.None;
            WindowState = WindowState.Maximized;
        }
        else
        {
            SystemDecorations = SystemDecorations.Full;
            WindowState = WindowState.Normal;
        }
    }
}
