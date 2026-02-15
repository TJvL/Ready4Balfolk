using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.UI.Views.Settings;

public sealed class LanguageDisplayConverter : IValueConverter
{
    public static readonly LanguageDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ApplicationLanguage lang
            ? lang switch
            {
                ApplicationLanguage.Dutch => "Nederlands",
                _ => "English"
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
