namespace Ready4Balfolk.Domain.Models.History;

/// <summary>A track the evening played, or reached and could not.</summary>
/// <remarks>
/// <see cref="FilePath" /> is what a played track is recognised by later, so it is kept in the
/// database, and it is left out of an exported night: an export goes to an organiser, and where
/// the file sits on the DJ's disk is no part of what happened that evening.
/// </remarks>
public sealed record TrackHistoryEntry(
    string FilePath,
    string Dance,
    string Artist,
    string Title,
    TimeSpan Duration,
    bool RandomlyAdded,
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null) : QueueHistoryEntry(CompletionStatus, StartedAt, FinishedAt);
