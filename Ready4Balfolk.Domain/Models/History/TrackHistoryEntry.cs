namespace Ready4Balfolk.Domain.Models.History;

public sealed record TrackHistoryEntry(
    string FilePath,
    string Dance,
    string Artist,
    string Title,
    TimeSpan Duration,
    bool RandomlyAdded,
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null) : QueueHistoryEntry(CompletionStatus, StartedAt);
