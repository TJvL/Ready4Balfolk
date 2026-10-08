using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Ready4Balfolk.UI.Views.Queue;

/// <summary>A length as minutes and seconds, the one way the application writes one.</summary>
public sealed class DurationFormatConverter : IValueConverter
{
    public static readonly DurationFormatConverter Instance = new();

    /// <summary>Minutes and two-digit seconds: "3:07", and "62:00" rather than "1:02:00".</summary>
    /// <remarks>
    /// Shared by everything that writes a length outside a binding, so the queue, the catalogue,
    /// the history, the transport and the projector cannot drift into five ways of writing one.
    /// </remarks>
    public static string Format(TimeSpan length) => $"{(int)length.TotalMinutes}:{length.Seconds:D2}";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TimeSpan length ? Format(length) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
