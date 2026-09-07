using System.Collections.Generic;
using System.Globalization;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>One dance, as the panel shows it.</summary>
/// <remarks>
/// The names are joined with middots rather than given a title and a subtitle, because none of them
/// outranks the others. Rendering one large and the rest small would be a claim the list does not
/// make.
/// </remarks>
public sealed class DanceCardViewModel(Dance dance, int trackCount)
{
    public string Slug { get; } = dance.Slug;

    public string NamesText { get; } = string.Join(" · ", dance.Names);

    /// <summary>What the dice on this card is called, which has to be this card's dance.</summary>
    /// <remarks>
    /// One card carries one of these and the panel draws a hundred cards, so a name that did not
    /// say which dance would read the same on every one of them.
    /// </remarks>
    public string PickName { get; } = string.Format(
        CultureInfo.CurrentCulture,
        UiStrings.DanceList_PickDanceName,
        string.Join(" · ", dance.Names));

    public IReadOnlyList<string> Tags { get; } = dance.Tags;

    public int TrackCount { get; } = trackCount;

    /// <summary>A dance nobody owns a recording of cannot be played, so it says so instead.</summary>
    public bool HasTracks { get; } = trackCount > 0;

    public bool HasTags { get; } = dance.Tags.Count > 0;
}
