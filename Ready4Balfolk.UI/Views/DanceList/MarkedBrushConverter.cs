using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>Shows which row a random pick is scoped to, and leaves the rest quiet.</summary>
public sealed class MarkedBrushConverter : IValueConverter
{
    public static readonly MarkedBrushConverter Instance = new();

    private static readonly IBrush Marked = Brushes.Goldenrod;
    private static readonly IBrush Unmarked = new SolidColorBrush(Colors.Gray, 0.55);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Marked : Unmarked;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
