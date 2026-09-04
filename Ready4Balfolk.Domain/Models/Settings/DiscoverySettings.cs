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
/// <para>
/// Each of the four mechanisms is switched on separately, and starts off. Most libraries are read
/// by one of them, and a rule the user cannot see is a rule they never agreed to: what is switched
/// off is not merely hidden, it does nothing. <see cref="InForce"/> is what discovery runs on.
/// </para>
/// </remarks>
public sealed record DiscoverySettings
{
    /// <summary>Nothing declared, which is what an unconfigured library gets.</summary>
    public static readonly DiscoverySettings Undeclared = new();

    /// <summary>Whether names are read by the patterns below.</summary>
    public bool UsesFileNamePatterns { get; init; }

    /// <summary>Whether the folders a file sits in say what it is.</summary>
    public bool UsesFolderRoles { get; init; }

    /// <summary>Whether the tag fields below are trusted rather than the built-in guesses.</summary>
    public bool UsesTagTrust { get; init; }

    /// <summary>Whether a custom tag is read as the dance.</summary>
    public bool UsesCustomDanceTag { get; init; }

    /// <summary>File name patterns, in order. The first one that matches a name whole wins.</summary>
    public IReadOnlyList<string> FileNamePatterns { get; init; } = [];

    /// <summary>What each folder level means, outermost first. Index 0 is level 1.</summary>
    public IReadOnlyList<FolderRole> FolderRoles { get; init; } = [];

    public TagTrust TagTrust { get; init; } = new();

    /// <summary>
    /// The name of a custom tag whose value is the dance, or null when none is declared.
    /// </summary>
    /// <remarks>
    /// Some libraries carry the dance in a free-form tag (an ID3v2 TXXX frame or a Xiph field), and
    /// what that tag is called is theirs. Naming it here is the declaration: the field is read
    /// whole, recognised or not, exactly like a trusted tag field.
    /// </remarks>
    public string? CustomDanceTag { get; init; }

    /// <summary>
    /// The same settings with everything that is switched off taken out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a switched-off section holds is kept, because a person who unticks a section to try
    /// another one has not thrown away what they wrote. It just does not count: this is what
    /// discovery and every preview of it are given, so a library read by its folders is never also
    /// matched by a stale pattern nobody can see.
    /// </para>
    /// <para>
    /// A method rather than a property, because these settings are written to disk as JSON and a
    /// property returning another one of these is a settings file that never finishes.
    /// </para>
    /// </remarks>
    public DiscoverySettings InForce() => new()
    {
        UsesFileNamePatterns = UsesFileNamePatterns,
        UsesFolderRoles = UsesFolderRoles,
        UsesTagTrust = UsesTagTrust,
        UsesCustomDanceTag = UsesCustomDanceTag,
        FileNamePatterns = UsesFileNamePatterns ? FileNamePatterns : [],
        FolderRoles = UsesFolderRoles ? FolderRoles : [],
        TagTrust = UsesTagTrust ? TagTrust : new TagTrust(),
        CustomDanceTag = UsesCustomDanceTag ? CustomDanceTag : null
    };

    /// <summary>True when the user has switched at least one way of reading their library on.</summary>
    public bool DeclaresAnything =>
        UsesFileNamePatterns || UsesFolderRoles || UsesTagTrust || UsesCustomDanceTag;

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
        && UsesFileNamePatterns == other.UsesFileNamePatterns
        && UsesFolderRoles == other.UsesFolderRoles
        && UsesTagTrust == other.UsesTagTrust
        && UsesCustomDanceTag == other.UsesCustomDanceTag
        && FileNamePatterns.SequenceEqual(other.FileNamePatterns, StringComparer.Ordinal)
        && FolderRoles.SequenceEqual(other.FolderRoles)
        && TagTrust == other.TagTrust
        && string.Equals(CustomDanceTag, other.CustomDanceTag, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(
        UsesFileNamePatterns,
        UsesFolderRoles,
        UsesTagTrust,
        UsesCustomDanceTag,
        FileNamePatterns.Count,
        FolderRoles.Count,
        TagTrust,
        CustomDanceTag);
}
