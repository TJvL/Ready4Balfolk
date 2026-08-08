using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>Emphasises the spelling currently being displayed, without calling it the right one.</summary>
public sealed class DisplayedNameWeightConverter : IValueConverter
{
    public static readonly DisplayedNameWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontWeight.SemiBold : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
