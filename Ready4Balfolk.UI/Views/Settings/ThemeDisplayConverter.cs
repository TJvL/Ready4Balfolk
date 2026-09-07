using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Settings;

public sealed class ThemeDisplayConverter : IValueConverter
{
    public static readonly ThemeDisplayConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ApplicationTheme theme
            ? theme switch
            {
                ApplicationTheme.Light => UiStrings.Settings_ThemeLight,
                ApplicationTheme.Dark => UiStrings.Settings_ThemeDark,
                _ => UiStrings.Settings_ThemeAuto
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
