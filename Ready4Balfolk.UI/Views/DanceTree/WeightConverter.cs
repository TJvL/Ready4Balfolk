using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.DanceTree;

public sealed class WeightConverter : IValueConverter
{
    public static readonly WeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? i : 0m;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal d ? Math.Max(0, (int)d) : 0;
}
