using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>A tag field a file can carry.</summary>
public enum TagField
{
    Title,

    Artist,

    AlbumArtist,

    Album,

    Comment
}

/// <remarks>
/// A value with no member falls through to the comment tag in both, so what a claim says it read
/// and what it actually read cannot disagree. The settings converter stops a hand-edited number
/// from arriving here, but a name that throws turns one bad entry into every file failing to load.
/// </remarks>
public static class TagFieldExtension
{
    extension(TagField tagField)
    {
        public string Name => tagField switch
        {
            TagField.Title => "title",
            TagField.Artist => "artist",
            TagField.AlbumArtist => "album artist",
            TagField.Album => "album",
            _ => "comment"
        };

        public string? ValueOf(TrackEvidence evidence) => tagField switch
        {
            TagField.Title => evidence.TagTitle,
            TagField.Artist => evidence.TagArtist,
            TagField.AlbumArtist => evidence.TagAlbumArtist,
            TagField.Album => evidence.TagAlbum,
            _ => evidence.TagComment
        };
    }
}

