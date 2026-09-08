using System.Collections.Generic;
using System.Globalization;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>One dance, as the panel shows it.</summary>
/// <remarks>
/// <para>
/// The names are joined with middots rather than given a title and a subtitle, because none of them
/// outranks the others. Rendering one large and the rest small would be a claim the list does not
/// make.
/// </para>
/// <para>
/// Everything that can change while the panel is open says so, because the card is kept rather than
/// replaced: the count arrives after the dance does, and a card that could only take it by being
/// built again would take the DJ's keyboard with it. The slug is the exception, and is what makes
/// this the same card.
/// </para>
/// </remarks>
#pragma warning disable CS8618 // Every property is set by the Show that the constructor makes.
public sealed partial class DanceCardViewModel : ReactiveObject, IKeepsItsPlace
{
    public DanceCardViewModel(Dance dance, int trackCount)
    {
        Slug = dance.Slug;
        Show(dance, trackCount);
    }

    public string Slug { get; }

    string IKeepsItsPlace.Key => Slug;

    [Reactive] public partial string NamesText { get; private set; }

    /// <summary>What the dice on this card is called, which has to be this card's dance.</summary>
    /// <remarks>
    /// One card carries one of these and the panel draws a hundred cards, so a name that did not
    /// say which dance would read the same on every one of them.
    /// </remarks>
    [Reactive] public partial string PickName { get; private set; }

    [Reactive] public partial IReadOnlyList<string> Tags { get; private set; }

    [Reactive] public partial int TrackCount { get; private set; }

    /// <summary>A dance nobody owns a recording of cannot be played, so it says so instead.</summary>
    [Reactive] public partial bool HasTracks { get; private set; }

    [Reactive] public partial bool HasTags { get; private set; }

    /// <summary>Takes what the panel knows now, in place, so the controls drawn from it stay.</summary>
    public void Show(Dance dance, int trackCount)
    {
        var names = string.Join(" · ", dance.Names);

        NamesText = names;
        PickName = string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_PickDanceName, names);
        Tags = dance.Tags;
        HasTags = dance.Tags.Count > 0;
        TrackCount = trackCount;
        HasTracks = trackCount > 0;
    }
}
