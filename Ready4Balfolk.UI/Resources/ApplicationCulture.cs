using System.Globalization;

namespace Ready4Balfolk.UI.Resources;

/// <summary>The culture the application speaks, for a date whose day or month is a name.</summary>
/// <remarks>
/// <para>
/// The language setting moves the UI culture and nothing else, so <see cref="UiStrings" /> answers
/// in it while <see cref="CultureInfo.CurrentCulture" /> stays the machine's. Left to the machine,
/// a Dutch application on an English laptop read "opgehaald 3 October 2026" and "Sat 3 Oct". A
/// date that is written with a name in it is part of the sentence around it, so it is formatted
/// with this instead.
/// </para>
/// <para>
/// Only for that. Numbers, times and anything parsed back keep the machine's culture, which is the
/// DJ's regional settings and not the application's to change.
/// </para>
/// </remarks>
internal static class ApplicationCulture
{
    /// <summary>The culture <see cref="UiStrings" /> is read in.</summary>
    public static CultureInfo Current => UiStrings.Culture ?? CultureInfo.CurrentUICulture;
}
