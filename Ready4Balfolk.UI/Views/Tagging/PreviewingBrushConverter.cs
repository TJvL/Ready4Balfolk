using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Views.Tagging;

/// <summary>Marks the one row that is currently being previewed.</summary>
public sealed class PreviewingBrushConverter : IValueConverter
{
    public static readonly PreviewingBrushConverter Instance = new();

    private static readonly IBrush Previewing = Brushes.MediumSeaGreen;
    private static readonly IBrush Idle = new SolidColorBrush(Colors.Gray, 0.6);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Previewing : Idle;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
