using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Discovery;

/// <summary>One example of what a pattern makes of a file, for reading before agreeing.</summary>
public sealed record PatternSampleRow(string FileName, string Dance, string Artist, string Title)
{
    public static PatternSampleRow From(PatternPreviewRow row) => new(
        row.FileName,
        row.Dance ?? string.Empty,
        row.Artist ?? string.Empty,
        row.Title ?? string.Empty);
}

/// <summary>A pattern the user has already declared, and what it is doing to the library.</summary>
public sealed partial class DeclaredPatternViewModel(PatternPreview preview) : ReactiveObject
{
    public string Text { get; } = preview.Pattern;

    public string Summary { get; } = DiscoveryText.Summarise(preview);

    public IReadOnlyList<PatternSampleRow> Samples { get; } =
        [.. preview.Matches.Select(PatternSampleRow.From)];

    /// <summary>Open by default for nothing: the numbers are the point, the rows are the evidence.</summary>
    [Reactive] public partial bool IsExpanded { get; set; }
}

/// <summary>One folder level, what is actually in it, and what the user says it means.</summary>
public sealed partial class FolderLevelViewModel : ReactiveObject
{
    public FolderLevelViewModel(FolderLevelPreview preview, FolderRole role)
    {
        Level = preview.Level;
        Role = role;
        LevelText = string.Format(CultureInfo.CurrentCulture, UiStrings.Discovery_LevelLabel, preview.Level);
        DepthText = string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_LevelDepth, preview.FilesAtThisDepth, preview.Total);
        Values = [.. preview.Values.Select(entry => string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_LevelValue, entry.Value, entry.Files))];
    }

    public int Level { get; }

    public string LevelText { get; }

    /// <summary>How many files are deep enough for this level to mean anything.</summary>
    public string DepthText { get; }

    /// <summary>What sits at this level, commonest first, so a role is given with eyes open.</summary>
    public IReadOnlyList<string> Values { get; }

    [Reactive] public partial FolderRole Role { get; set; }

    public IReadOnlyList<FolderRole> AvailableRoles { get; } = [.. System.Enum.GetValues<FolderRole>()];
}

/// <summary>One tag field, and whether it may speak for a field of the track.</summary>
public sealed partial class TagFieldToggle(TagField field) : ReactiveObject
{
    public TagField Field { get; } = field;

    public string Label { get; } = DiscoveryText.NameOf(field);

    [Reactive] public partial bool IsTrusted { get; set; }
}

/// <summary>Which tag fields speak for one field of the track, and whether the user said so.</summary>
public sealed partial class TagTrustFieldViewModel : ReactiveObject
{
    public TagTrustFieldViewModel(TrackField field, string label, IReadOnlyList<TagField>? declared)
    {
        Field = field;
        Label = label;
        UsesDefault = declared is null;

        var defaults = field switch
        {
            TrackField.Artist => TagTrust.DefaultForArtist,
            TrackField.Title => TagTrust.DefaultForTitle,
            _ => TagTrust.DefaultForDance
        };

        var trusted = declared ?? defaults;
        Toggles =
        [
            .. System.Enum.GetValues<TagField>()
                .Select(tag => new TagFieldToggle(tag) { IsTrusted = trusted.Contains(tag) })
        ];
    }

    public TrackField Field { get; }

    public string Label { get; }

    /// <summary>
    /// True while the built-in guess applies. Turning it off is the declaration, which is why the
    /// toggles below it mean nothing until it is.
    /// </summary>
    [Reactive] public partial bool UsesDefault { get; set; }

    public IReadOnlyList<TagFieldToggle> Toggles { get; }

    /// <summary>What to store: null while the default applies, the chosen list once it does not.</summary>
    public IReadOnlyList<TagField>? Declared => UsesDefault
        ? null
        : [.. Toggles.Where(toggle => toggle.IsTrusted).Select(toggle => toggle.Field)];
}

/// <summary>The words this screen puts on numbers and enums.</summary>
internal static class DiscoveryText
{
    public static string Summarise(PatternPreview preview) => preview.Problem switch
    {
        PatternProblem.None => string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_PatternMatches, preview.Matched, preview.Total, preview.Missed),
        PatternProblem.Empty => string.Empty,
        PatternProblem.UnknownToken => UiStrings.Discovery_ProblemUnknownToken,
        PatternProblem.NoFields => UiStrings.Discovery_ProblemNoFields,
        PatternProblem.AdjacentFields => UiStrings.Discovery_ProblemAdjacentFields,
        _ => UiStrings.Discovery_ProblemDuplicateField
    };

    public static string NameOf(TagField field) => field switch
    {
        TagField.Title => UiStrings.Discovery_TagTitle,
        TagField.Artist => UiStrings.Discovery_TagArtist,
        TagField.AlbumArtist => UiStrings.Discovery_TagAlbumArtist,
        TagField.Album => UiStrings.Discovery_TagAlbum,
        _ => UiStrings.Discovery_TagComment
    };
}
