using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.DanceTree;

public sealed class BoolToStringConverter : IValueConverter
{
    public string? TrueValue { get; set; }
    public string? FalseValue { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
