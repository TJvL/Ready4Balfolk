namespace Ready4Balfolk.Domain.Models.History;

/// <summary>The evening was called, and this is when.</summary>
/// <remarks>
/// An evening that simply stops at the last track reads as one that was abandoned; an evening with
/// an end in it reads as one that finished.
/// </remarks>
public sealed record EndOfNightHistoryEntry(
    TimeSpan? Duration,
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null) : QueueHistoryEntry(CompletionStatus, StartedAt);
