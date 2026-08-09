using System.Globalization;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Tagging;

/// <summary>A dance offered in a drop-down, labelled with how much the library already plays it.</summary>
public sealed record DanceSuggestionRow(DanceSuggestion Suggestion)
{
    public string Label => Suggestion.TrackCount > 0
        ? string.Format(CultureInfo.CurrentCulture, UiStrings.Tagging_SuggestionWithCount,
            Suggestion.DisplayName, Suggestion.TrackCount)
        : Suggestion.DisplayName;
}
