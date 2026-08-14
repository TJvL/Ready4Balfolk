using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>The user's declared settings, compiled once so a scan can run them over every file.</summary>
/// <remarks>
/// Patterns are settings text on disk and compiled objects here. A pattern that will not compile is
/// dropped rather than carried as a rule that silently never matches: the settings screen refuses
/// it while it is being written, so one reaching this point is a hand-edited file.
/// </remarks>
public sealed class DeclaredDiscovery
{
    private DeclaredDiscovery(
        IReadOnlyList<FileNamePattern> patterns,
        IReadOnlyList<FolderRole> folderRoles,
        TagTrust tagTrust,
        string? customDanceTag)
    {
        Patterns = patterns;
        FolderRoles = folderRoles;
        TagTrust = tagTrust;
        CustomDanceTag = customDanceTag;
    }

    /// <summary>Nothing declared: what discovery runs on until a user says otherwise.</summary>
    public static DeclaredDiscovery Undeclared { get; } = Compile(DiscoverySettings.Undeclared);

    public IReadOnlyList<FileNamePattern> Patterns { get; }

    public IReadOnlyList<FolderRole> FolderRoles { get; }

    public TagTrust TagTrust { get; }

    /// <summary>The custom tag declared to hold the dance, or null when none is.</summary>
    public string? CustomDanceTag { get; }

    public static DeclaredDiscovery Compile(DiscoverySettings settings) => new(
        [.. settings.FileNamePatterns.Select(text => FileNamePattern.Parse(text).Pattern).OfType<FileNamePattern>()],
        settings.FolderRoles,
        settings.TagTrust,
        string.IsNullOrWhiteSpace(settings.CustomDanceTag) ? null : settings.CustomDanceTag.Trim());

    /// <summary>The role declared for a level, counted from 1 outermost.</summary>
    public FolderRole RoleForLevel(int level) =>
        level >= 1 && level <= FolderRoles.Count ? FolderRoles[level - 1] : FolderRole.Unknown;

    /// <summary>
    /// What the first pattern to match this name whole makes of it, or null when none does.
    /// </summary>
    /// <remarks>
    /// First match wins, in the order the user put them in. Ordering is the user's tool for saying
    /// which of two overlapping shapes their library means, and trying every pattern and merging
    /// the results would take that away from them.
    /// </remarks>
    public FileNamePatternMatch? MatchFileName(string withExtension, string withoutExtension)
    {
        foreach (var pattern in Patterns)
        {
            if (pattern.Match(pattern.UsesExtension ? withExtension : withoutExtension) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
