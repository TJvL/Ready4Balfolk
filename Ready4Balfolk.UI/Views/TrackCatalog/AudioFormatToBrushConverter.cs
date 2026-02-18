using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public sealed class AudioFormatToBrushConverter : IValueConverter
{
    public static readonly AudioFormatToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is AudioFormat format
            ? format switch
            {
                AudioFormat.Mp3 => "FormatMp3Brush",
                AudioFormat.Wav => "FormatWavBrush",
                AudioFormat.Flac => "FormatFlacBrush",
                AudioFormat.Ogg => "FormatOggBrush",
                AudioFormat.Aif => "FormatAifBrush",
                _ => null
            }
            : null;

        return key != null
               && Application.Current!.TryFindResource(key, Application.Current?.ActualThemeVariant, out var resource)
            ? (IBrush)resource!
            : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
