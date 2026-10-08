using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>What a tag field is called in a claim, and what it holds for one file.</summary>
/// <remarks>
/// Here rather than beside the enum, because reading a field means knowing what a file offered,
/// and a setting has no business knowing that. A value with no member falls through to the comment
/// tag in both, so what a claim says it read and what it actually read cannot disagree. The
/// settings converter stops a hand-edited number from arriving here, but a name that throws turns
/// one bad entry into every file failing to load.
/// </remarks>
public static class TagFieldExtensions
{
    /// <summary>The field as a claim names its source.</summary>
    public static string Name(this TagField tagField) => tagField switch
    {
        TagField.Title => "title",
        TagField.Artist => "artist",
        TagField.AlbumArtist => "album artist",
        TagField.Album => "album",
        _ => "comment"
    };

    /// <summary>What this file's tag of that field says, or null when it says nothing.</summary>
    public static string? ValueOf(this TagField tagField, TrackEvidence evidence) => tagField switch
    {
        TagField.Title => evidence.TagTitle,
        TagField.Artist => evidence.TagArtist,
        TagField.AlbumArtist => evidence.TagAlbumArtist,
        TagField.Album => evidence.TagAlbum,
        _ => evidence.TagComment
    };
}
