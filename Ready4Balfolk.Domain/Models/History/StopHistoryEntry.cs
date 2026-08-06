namespace Ready4Balfolk.Domain.Models.History;

public sealed record StopHistoryEntry(
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null) : QueueHistoryEntry(CompletionStatus, StartedAt);
