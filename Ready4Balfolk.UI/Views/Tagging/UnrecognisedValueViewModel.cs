using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Tagging;

/// <summary>Anything in the editor that can be answered, and shows that it has been.</summary>
/// <remarks>
/// An answered row stays exactly where it is. Removing it the moment it is answered leaves no way to
/// see what was decided or to correct a mis-click, and it makes every row below it jump under the
/// pointer.
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public abstract partial class DecidableRow : ReactiveObject
{
    protected DecidableRow()
    {
        _isDecidedHelper = this.WhenAnyValue(x => x.DecidedAs, x => x.IsIgnored,
                (decided, ignored) => decided is not null || ignored)
            .ToProperty(this, x => x.IsDecided);

        _decisionTextHelper = this.WhenAnyValue(x => x.DecidedAs, x => x.IsIgnored,
                (decidedAs, ignored) => ignored
                    ? UiStrings.Tagging_DecidedNotADance
                    : decidedAs is null
                        ? string.Empty
                        : string.Format(CultureInfo.CurrentCulture, UiStrings.Tagging_DecidedAs, decidedAs))
            .ToProperty(this, x => x.DecisionText);
    }

    /// <summary>What this was set to, or null while it is still a question.</summary>
    [Reactive] public partial string? DecidedAs { get; set; }

    /// <summary>True when it was answered with "not a dance" rather than with a dance.</summary>
    [Reactive] public partial bool IsIgnored { get; set; }

    [ObservableAsProperty] public partial bool IsDecided { get; }

    [ObservableAsProperty] public partial string DecisionText { get; }
}

/// <summary>One row of the editor: a distinct thing the library claims that nothing recognised.</summary>
public sealed partial class UnrecognisedValueViewModel(UnrecognisedValue value) : DecidableRow
{
    public UnrecognisedValue Value { get; } = value;

    public string Text => Value.Value;

    public int TrackCount => Value.TrackCount;

    public string TrackCountText =>
        string.Format(CultureInfo.CurrentCulture, UiStrings.Tagging_TrackCount, Value.TrackCount);

    public IReadOnlyList<DanceSuggestionRow> Suggestions { get; } =
        [.. value.Suggestions.Select(suggestion => new DanceSuggestionRow(suggestion))];

    public IReadOnlyList<FolderGroupViewModel> Folders { get; } =
        [.. value.Folders.Select(folder => new FolderGroupViewModel(folder))];

    public IReadOnlyList<PreviewRowViewModel> Tracks { get; } =
        [.. value.Paths.Select(path => new PreviewRowViewModel(path))];

    /// <summary>
    /// Whether one decision settles every track. False for a value that is too general, and the view
    /// shows no map control at all in that case.
    /// </summary>
    public bool CanMapAsAWhole => Value.CanMapAsAWhole;

    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>True when the only sensible move is to look at the folders instead.</summary>
    public bool IsTooGeneral => Value.Kind == UnrecognisedKind.TooGeneral;

    /// <summary>Ambiguous values are decided per track, so their track list opens by default.</summary>
    public bool IsAmbiguous => Value.Kind == UnrecognisedKind.Ambiguous;

    public string Explanation => Value.Kind switch
    {
        UnrecognisedKind.Misspelled => UiStrings.Tagging_LooksLikeAMisspelling,
        UnrecognisedKind.TooGeneral => UiStrings.Tagging_TooGeneralExplanation,
        UnrecognisedKind.Ambiguous => UiStrings.Tagging_AmbiguousExplanation,
        _ => UiStrings.Tagging_NothingLooksLikeIt
    };

    /// <summary>Whether the tracks and folders under this value are showing.</summary>
    [Reactive] public partial bool IsExpanded { get; set; }
}

/// <summary>The tracks of one value that sit in a single folder.</summary>
public sealed partial class FolderGroupViewModel(FolderBreakdown breakdown) : DecidableRow
{
    public FolderBreakdown Breakdown { get; } = breakdown;

    public string FolderName { get; } = System.IO.Path.GetFileName(breakdown.FolderKey) is { Length: > 0 } name
        ? name
        : breakdown.FolderKey;

    public IReadOnlyList<DanceSuggestionRow> Suggestions { get; } =
        [.. breakdown.Suggestions.Select(suggestion => new DanceSuggestionRow(suggestion))];

    public IReadOnlyList<PreviewRowViewModel> Tracks { get; } =
        [.. breakdown.Paths.Select(path => new PreviewRowViewModel(path))];

    public int TrackCount => Breakdown.Paths.Count;

    public string Summary => Breakdown.Suggestions.Count > 0
        ? string.Format(CultureInfo.CurrentCulture, UiStrings.Tagging_FolderAgrees,
            Breakdown.Paths.Count, Breakdown.Suggestions[0].DisplayName, Breakdown.Suggestions[0].TrackCount)
        : string.Format(CultureInfo.CurrentCulture, UiStrings.Tagging_FolderSaysNothing, Breakdown.Paths.Count);

    /// <summary>False when the folder has nothing to offer, so its tracks stay per track.</summary>
    public bool HasSuggestions => Suggestions.Count > 0;
}

/// <summary>One track, with somewhere to show that it is playing and what it was set to.</summary>
public sealed partial class PreviewRowViewModel(string path) : DecidableRow
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileNameWithoutExtension(path);

    public string FolderName { get; } =
        System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? string.Empty);

    [Reactive] public partial bool IsPreviewing { get; set; }
}
