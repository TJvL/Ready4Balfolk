using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Everything one file has to say about itself, before anything is decided.</summary>
/// <remarks>
/// Gathering and deciding are kept apart on purpose. Gathering opens a file; deciding is a pure
/// function of what was gathered plus the dance list, so it can be re-run when the list changes,
/// and tested without a file existing at all.
/// </remarks>
public sealed record TrackEvidence
{
    public required string FileNameWithoutExtension { get; init; }

    /// <summary>The folders between the music directory and the file, outermost first.</summary>
    public required IReadOnlyList<string> PathSegments { get; init; }

    public string? TagTitle { get; init; }

    public string? TagArtist { get; init; }

    public string? TagAlbumArtist { get; init; }

    public string? TagAlbum { get; init; }

    public string? TagGenre { get; init; }

    public string? TagComment { get; init; }

    public required TimeSpan Duration { get; init; }

    public required AudioFormat Format { get; init; }

    public required byte[] ContentHash { get; init; }

    /// <summary>The album folder, which is what "the rest of this album" is decided over.</summary>
    public string? AlbumFolderKey => PathSegments.Count == 0 ? null : string.Join('/', PathSegments);
}
