using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>How a value came to be agreed to, which decides what happens to it later.</summary>
public enum ApprovalKind
{
    /// <summary>
    /// A rule the user greenlit answered it.
    /// </summary>
    /// <remarks>
    /// The user vouched for the rule, not for each of the two thousand files it touched, so changing
    /// the rules takes the approval back with them and the tracks return to review. Fixing a pattern
    /// greenlit by mistake has to undo its work.
    /// </remarks>
    ByRule,

    /// <summary>
    /// The user looked at this track and said yes.
    /// </summary>
    /// <remarks>
    /// Sticky. Nothing overwrites it: not a rescan, not a retag, not a rule change, and not this
    /// application writing the file's own tags. It is the one thing in the index that was not
    /// derived from anything.
    /// </remarks>
    Individual
}

/// <summary>One field of one track, agreed to by a person.</summary>
/// <remarks>
/// <para>
/// Stored apart from the derived values on purpose. Everything in <see cref="LibraryEntry"/> is
/// what the application worked out and is free to be overwritten by the next scan; this is what a
/// person decided, and overwriting it would be losing the only thing here that cannot be recomputed.
/// </para>
/// <para>
/// Keyed on the content hash, which covers the audio alone, so a retag or a rename keeps the
/// approval. The dance is stored as the slug when the published list knows the value, because the
/// slug is the identity and the names on it are a flat set of equals the list may re-spell. Text is
/// kept only for a value the list does not know yet: that is exactly what parks a track, and keeping
/// it is what lets a later import of the list release it without anybody being asked twice.
/// </para>
/// </remarks>
public sealed record TrackApproval
{
    public required byte[] ContentHash { get; init; }

    public required TrackField Field { get; init; }

    /// <summary>The value that was agreed to.</summary>
    public required string Value { get; init; }

    public required ApprovalKind Kind { get; init; }

    /// <summary>Which rule did it, so review can say so. Null for an individual approval.</summary>
    public string? Rule { get; init; }

    /// <summary>
    /// The file as it stood when this was agreed to.
    /// </summary>
    /// <remarks>
    /// A retag keeps the approval and still deserves attention: the track is marked as changed since
    /// it was approved and comes back for reconfirmation, with the value preserved. Comparing the
    /// file's write time is what tells the two apart, and it is read off the file rather than off a
    /// clock, so it means the same thing on every machine.
    /// </remarks>
    public required DateTime FileWriteUtc { get; init; }
}
