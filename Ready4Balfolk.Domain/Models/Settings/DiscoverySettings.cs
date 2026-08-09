namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>What the user has told the application about the shape of their library.</summary>
/// <remarks>
/// <para>
/// Empty by default, and that default is the honest one: a library root is a black box, and there
/// is no arrangement common enough to assume on somebody's behalf. Everything here is the user
/// stating a rule, which is the one thing code cannot work out for itself. Code can measure that
/// strings agree; only a person can say that a rule is right.
/// </para>
/// <para>
/// A declaration is therefore also a bulk approval, so nothing here should ever be filled in on the
/// user's behalf, and never without showing them what it does to their library first.
/// </para>
/// </remarks>
public sealed record DiscoverySettings
{
    /// <summary>Nothing declared, which is what an unconfigured library gets.</summary>
    public static readonly DiscoverySettings Undeclared = new();

    /// <summary>File name patterns, in order. The first one that matches a name whole wins.</summary>
    public IReadOnlyList<string> FileNamePatterns { get; init; } = [];

    /// <summary>What each folder level means, outermost first. Index 0 is level 1.</summary>
    public IReadOnlyList<FolderRole> FolderRoles { get; init; } = [];

    public TagTrust TagTrust { get; init; } = new();

    /// <summary>True when the user has stated anything at all.</summary>
    public bool DeclaresAnything =>
        FileNamePatterns.Count > 0
        || FolderRoles.Any(role => role is not FolderRole.Unknown)
        || TagTrust.IsDeclared;

    /// <summary>The role declared for a level, counted from 1 outermost.</summary>
    public FolderRole RoleForLevel(int level) =>
        level >= 1 && level <= FolderRoles.Count ? FolderRoles[level - 1] : FolderRole.Unknown;

    /// <summary>
    /// Compares what was declared rather than which list objects hold it.
    /// </summary>
    /// <remarks>
    /// A record compares its collection members by reference, so two settings saying exactly the
    /// same thing would come out different and re-read the whole library every time anything else
    /// in the settings file was touched.
    /// </remarks>
    public bool Equals(DiscoverySettings? other) =>
        other is not null
        && FileNamePatterns.SequenceEqual(other.FileNamePatterns, StringComparer.Ordinal)
        && FolderRoles.SequenceEqual(other.FolderRoles)
        && TagTrust == other.TagTrust;

    public override int GetHashCode() => HashCode.Combine(FileNamePatterns.Count, FolderRoles.Count, TagTrust);
}
