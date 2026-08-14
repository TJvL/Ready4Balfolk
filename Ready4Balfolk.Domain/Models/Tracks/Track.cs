using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Models.Tracks;

public sealed record Track(string Dance, string Artist, string Title, IFileInfo FileInfo, TimeSpan Length, AudioFormat Format)
{
    /// <summary>What the file itself claims, before the dance list had a say.</summary>
    public string OriginalDance { get; init; } = Dance;

    /// <summary>
    /// The dance this track resolved to, or null when the list does not know the name.
    /// </summary>
    /// <remarks>
    /// The slug, not a name: it survives respelling, reordering and renaming, so a track keeps
    /// pointing at the same dance however the user edits their list. <see cref="Dance"/> is only
    /// what that slug is currently displayed as.
    /// </remarks>
    public string? DanceSlug { get; init; }
}
