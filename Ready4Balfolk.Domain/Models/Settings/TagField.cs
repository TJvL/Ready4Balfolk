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
            TagField.Comment => "comment",
            _ => throw new ArgumentOutOfRangeException(nameof(tagField))
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

