namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>What the user says one level of folder means, counted outermost first.</summary>
/// <remarks>
/// A role is only ever applied where the depth is actually there. A library with a level 3 in one
/// corner and none in another is ordinary, and a rule that fires on the files that have the depth
/// and stays quiet on the rest is the honest reading of it.
/// </remarks>
public enum FolderRole
{
    /// <summary>Nothing is claimed about this level. The default, and what a library says by itself.</summary>
    Unknown,

    Artist,

    /// <summary>
    /// Grouping only. Nothing reads an album today, but saying a level is one is still worth
    /// recording: it is a level that is not the artist and not the dance.
    /// </summary>
    Album,

    Dance,

    /// <summary>Deliberately meaningless: a "Music" or "CD1" level that says nothing about anything.</summary>
    Ignore
}
