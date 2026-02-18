using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public sealed class AudioFormatToIconConverter : IValueConverter
{
    public static readonly AudioFormatToIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AudioFormat format)
        {
            return null;
        }

        var key = format switch
        {
            AudioFormat.Mp3 => "IconFormatMp3",
            AudioFormat.Wav => "IconFormatWav",
            AudioFormat.Flac => "IconFormatFlac",
            AudioFormat.Ogg => "IconFormatOgg",
            AudioFormat.Aif => "IconFormatAif",
            _ => null
        };

        return key != null
               && Application.Current!.TryFindResource(key, Application.Current?.ActualThemeVariant, out var resource)
            ? (StreamGeometry)resource!
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
