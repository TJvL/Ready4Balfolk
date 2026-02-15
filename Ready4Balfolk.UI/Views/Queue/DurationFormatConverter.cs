using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.Queue;

public sealed class DurationFormatConverter : IValueConverter
{
    public static readonly DurationFormatConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            TimeSpan ts => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}",
            _ => ""
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
