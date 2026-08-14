using System.Collections.Generic;
using Ready4Balfolk.Domain.Models.Dances;

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

    public IReadOnlyList<string> Tags { get; } = dance.Tags;

    public int TrackCount { get; } = trackCount;

    /// <summary>A dance nobody owns a recording of cannot be played, so it says so instead.</summary>
    public bool HasTracks { get; } = trackCount > 0;

    public bool HasTags { get; } = dance.Tags.Count > 0;
}
