using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;

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

    /// <summary>
    /// What answered each field, and how well.
    /// </summary>
    /// <remarks>
    /// Kept because review has to show a value next to where it came from: a wrong artist is only
    /// obvious when it says it was read off a folder name. It is also what orders the queue, since
    /// a corroborated value and a lone guess are not equally worth a person's attention.
    /// </remarks>
    public DerivedFrom Dance { get; init; } = DerivedFrom.Nothing;

    public DerivedFrom ArtistFrom { get; init; } = DerivedFrom.Nothing;

    public DerivedFrom TitleFrom { get; init; } = DerivedFrom.Nothing;

    public DerivedFrom From(TrackField field) => field switch
    {
        TrackField.Dance => Dance,
        TrackField.Artist => ArtistFrom,
        _ => TitleFrom
    };
}

/// <summary>Where a derived value came from, and on what grounds.</summary>
/// <param name="Kind">Which independent reading answered, if any did.</param>
/// <param name="Detail">Which part of it, in the words the collector used.</param>
/// <param name="Reason">How it was decided, which is the whole of how confident it is.</param>
public sealed record DerivedFrom(ClaimSourceKind? Kind, string? Detail, DecisionReason Reason)
{
    public static readonly DerivedFrom Nothing = new(null, null, DecisionReason.NoClaim);
}
