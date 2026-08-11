using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.Settings;

/// <summary>
/// Bridges the cutoff time, stored as minutes since midnight, to the TimeSpan a TimePicker wants.
/// </summary>
public sealed class MinutesOfDayConverter : IValueConverter
{
    public static readonly MinutesOfDayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int minutes
            ? TimeSpan.FromMinutes(Math.Clamp(minutes, 0, (24 * 60) - 1))
            : TimeSpan.Zero;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TimeSpan time
            ? Math.Clamp((int)time.TotalMinutes, 0, (24 * 60) - 1)
            : 0;
}
