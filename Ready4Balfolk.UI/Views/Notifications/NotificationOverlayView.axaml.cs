using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Notifications;

public partial class NotificationOverlayView : UserControl
{
    private readonly NotificationService _service;

    public NotificationOverlayView()
    {
        InitializeComponent();
        _service = App.Services.GetRequiredService<NotificationService>();
        DataContext = _service;
    }

    private void OnNotificationPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: NotificationItem item })
        {
            _service.Dismiss(item);
            e.Handled = true;
        }
    }
}
