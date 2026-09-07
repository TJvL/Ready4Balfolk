using System.Globalization;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Views.Settings;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The theme combo box shows real, localized words rather than the raw
/// <see cref="ApplicationTheme"/> enum names, which read the same in every language.
/// </summary>
public sealed class ThemeDisplayConverterTests
{
    [Theory]
    [InlineData(ApplicationTheme.Light, nameof(UiStrings.Settings_ThemeLight))]
    [InlineData(ApplicationTheme.Dark, nameof(UiStrings.Settings_ThemeDark))]
    [InlineData(ApplicationTheme.Automatic, nameof(UiStrings.Settings_ThemeAuto))]
    public void Convert_ReturnsTheMatchingResourceString(ApplicationTheme theme, string expectedResourceKey)
    {
        var expected = UiStrings.ResourceManager.GetString(expectedResourceKey, CultureInfo.InvariantCulture);

        var result = ThemeDisplayConverter.Instance.Convert(theme, typeof(string), null, CultureInfo.InvariantCulture);

        // ApplicationTheme.Automatic must not surface as its own enum name: the resource
        // for it ("Auto") is deliberately not the same word, so a naive ToString() fallback
        // would fail this assertion.
        Assert.Equal(expected, result);
    }
}
