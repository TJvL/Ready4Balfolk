using System.Globalization;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// A few things the DJ can read that used to be literals baked into a view rather than resources:
/// the "no track loaded" placeholder and the presentation window's title before it is set for a
/// real display. Both need an entry in every language the app ships, not just English.
/// </summary>
public sealed class UiStringsResourceTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("nl")]
    public void ThePlaybackPlaceholder_HasAnEntryInEveryLanguage(string language)
    {
        var value = UiStrings.ResourceManager.GetString(
            "Playback_NoTrackPlaceholder", CultureInfo.GetCultureInfo(language));

        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Theory]
    [InlineData("en", "Presentation Display")]
    [InlineData("nl", "Presentatiescherm")]
    public void ThePresentationWindowsDefaultTitle_HasAnEntryInEveryLanguage(string language, string expected)
    {
        var value = UiStrings.ResourceManager.GetString(
            "Presentation_WindowTitleDefault", CultureInfo.GetCultureInfo(language));

        Assert.Equal(expected, value);
    }
}
