using System.Globalization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.History;

/// <summary>One night to choose between: tonight, or an evening that has been filed.</summary>
/// <remarks>
/// A record, so the list can be rebuilt and the same night stay selected: what is on screen must
/// not jump to another evening because a track finished in this one.
/// </remarks>
public sealed record NightOption(long Id, string Label, bool IsTonight)
{
    /// <summary>The night that is running, which is where the application starts.</summary>
    public static NightOption Tonight(long id) => new(id, UiStrings.History_Tonight, true);

    public static NightOption For(NightSummary night) => new(
        night.Id,
        string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.History_NightOn,
            night.StartedAt.ToString("ddd d MMM", CultureInfo.CurrentCulture),
            night.StartedAt.ToString("HH:mm", CultureInfo.CurrentCulture)),
        false);

    public override string ToString() => Label;
}
