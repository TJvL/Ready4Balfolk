using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>One row of the library index: what is known about a file without opening it.</summary>
/// <remarks>
/// Keyed by <see cref="ContentHash"/>, a hash of the audio stream alone. Editing a tag or renaming
/// the file changes neither the audio nor the row it belongs to, so a track keeps whatever the user
/// has decided about it.
/// </remarks>
public sealed record LibraryEntry
{
    public required byte[] ContentHash { get; init; }

    public required string Path { get; init; }

    /// <summary>Size and write time together are the cheap check for "this file has not changed".</summary>
    public required long FileSize { get; init; }

    public required DateTime LastWriteUtc { get; init; }

    public required TimeSpan Duration { get; init; }

    public required AudioFormat Format { get; init; }

    /// <summary>
    /// The dance this file resolved to, as a slug. Null means nothing has recognised it yet, which
    /// is a real state and the one the tagging editor works through.
    /// </summary>
    public string? DanceSlug { get; init; }

    /// <summary>What the file itself claimed the dance was, kept for the tagging editor to group by.</summary>
    public string? OriginalDance { get; init; }

    public string? Artist { get; init; }

    public string? Title { get; init; }
}
