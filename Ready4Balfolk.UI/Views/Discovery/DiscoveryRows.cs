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
public sealed partial class FolderLevelViewModel(FolderLevelPreview preview, FolderRole role) : ReactiveObject
{
    public int Level { get; } = preview.Level;

    public string LevelText { get; } = string.Format(CultureInfo.CurrentCulture, UiStrings.Discovery_LevelLabel, preview.Level);

    /// <summary>How many files are deep enough for this level to mean anything.</summary>
    public string DepthText { get; } = string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_LevelDepth, preview.FilesAtThisDepth, preview.Total);

    /// <summary>What sits at this level, commonest first, so a role is given with eyes open.</summary>
    public IReadOnlyList<string> Values { get; } = [.. preview.Values.Select(entry => string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_LevelValue, entry.Value, entry.Files))];

    [Reactive] public partial FolderRole Role { get; set; } = role;

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
        Toggles = [.. System.Enum.GetValues<TagField>().Select(tag => new TagFieldToggle(tag))];
        ShowDeclared(declared);
    }

    /// <summary>
    /// Shows a stored declaration, or the default when there is none.
    /// </summary>
    /// <remarks>
    /// The toggles as well as the checkbox: syncing only <see cref="UsesDefault"/> leaves the
    /// toggles empty after a restart, and saving the screen then persists an empty declaration
    /// over what the user actually stated.
    /// </remarks>
    public void ShowDeclared(IReadOnlyList<TagField>? declared)
    {
        UsesDefault = declared is null;

        var trusted = declared ?? Defaults(Field);
        foreach (var toggle in Toggles)
        {
            toggle.IsTrusted = trusted.Contains(toggle.Field);
        }
    }

    private static IReadOnlyList<TagField> Defaults(TrackField field) => field switch
    {
        TrackField.Artist => TagTrust.DefaultForArtist,
        TrackField.Title => TagTrust.DefaultForTitle,
        _ => TagTrust.DefaultForDance
    };

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

/// <summary>
/// Something the library's own strings suggest, with the evidence and no consequences.
/// </summary>
/// <remarks>
/// A proposal, never a decision. Accepting one writes a declared setting, which approves every file
/// it matches in one act, so it is the user's to give and the numbers behind it are the point.
/// </remarks>
public sealed partial class ProposalViewModel : ReactiveObject
{
    private ProposalViewModel(string headline, IReadOnlyList<string> evidence, IReadOnlyList<string> samples)
    {
        Headline = headline;
        Evidence = evidence;
        Samples = samples;
    }

    public string Headline { get; }

    /// <summary>What was measured, so a person can disagree with the reasoning and not just the answer.</summary>
    public IReadOnlyList<string> Evidence { get; }

    public IReadOnlyList<string> Samples { get; }

    /// <summary>The rule this would declare, when it can be declared at all.</summary>
    public string? Pattern { get; private init; }

    public int Level { get; private init; }

    public FolderRole Role { get; private init; }

    public bool IsFolderRole { get; private init; }

    /// <summary>False for a shape nothing could be named in: worth seeing, not worth declaring.</summary>
    public bool CanAccept => Pattern is not null || IsFolderRole;

    [Reactive] public partial bool IsExpanded { get; set; }

    public static ProposalViewModel From(ShapeProposal shape) => new(
        string.Format(
            CultureInfo.CurrentCulture,
            shape.Pattern is null ? UiStrings.Discovery_ProposalShapeUnnamed : UiStrings.Discovery_ProposalShape,
            shape.Files,
            shape.Considered,
            shape.Pattern ?? string.Empty),
        [.. shape.Positions.Select(Describe)],
        shape.Samples)
    {
        Pattern = shape.Pattern
    };

    public static ProposalViewModel From(FolderRoleProposal folder) => new(
        string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.Discovery_ProposalLevel,
            folder.Level,
            DiscoveryText.NameOf(folder.Role),
            folder.Agreeing,
            folder.Considered),
        [],
        folder.Samples)
    {
        Level = folder.Level,
        Role = folder.Role,
        IsFolderRole = true
    };

    /// <summary>One position, in the numbers that named it or failed to.</summary>
    private static string Describe(PositionFinding position) => string.Format(
        CultureInfo.CurrentCulture,
        UiStrings.Discovery_ProposalPosition,
        position.Position,
        position.Field is { } field ? DiscoveryText.NameOf(field) : UiStrings.Discovery_ProposalNothing,
        position.DanceNames,
        position.Distinct,
        position.AgreesWithTag,
        position.Files);
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

    public static string NameOf(TrackField field) => field switch
    {
        TrackField.Dance => UiStrings.Discovery_FieldDance,
        TrackField.Artist => UiStrings.Discovery_FieldArtist,
        _ => UiStrings.Discovery_FieldTitle
    };

    public static string NameOf(FolderRole role) => role switch
    {
        FolderRole.Artist => UiStrings.Discovery_RoleArtist,
        FolderRole.Album => UiStrings.Discovery_RoleAlbum,
        FolderRole.Dance => UiStrings.Discovery_RoleDance,
        FolderRole.Ignore => UiStrings.Discovery_RoleIgnore,
        _ => UiStrings.Discovery_RoleUnknown
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
