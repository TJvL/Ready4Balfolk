using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Notifications;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public static readonly SeverityToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is NotificationSeverity severity
            ? severity switch
            {
                NotificationSeverity.Error => "NotificationErrorBrush",
                NotificationSeverity.Warning => "NotificationWarningBrush",
                NotificationSeverity.Information => "NotificationInfoBrush",
                _ => throw new ArgumentOutOfRangeException(nameof(value), value,
                    "no case implemented for this notification severity")
            }
            : "NotificationInfoBrush";

        return Application.Current!.TryFindResource(key, Application.Current?.ActualThemeVariant, out var resource)
            ? (IBrush)resource!
            : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
