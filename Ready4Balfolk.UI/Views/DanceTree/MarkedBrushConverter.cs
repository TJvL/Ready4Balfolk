using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Ready4Balfolk.UI.Views.DanceTree;

public sealed class MarkedBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "MarkedActiveBrush" : "MarkedMutedBrush";

        return Application.Current!.TryFindResource(key, Application.Current?.ActualThemeVariant, out var resource)
            ? (IBrush)resource!
            : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
