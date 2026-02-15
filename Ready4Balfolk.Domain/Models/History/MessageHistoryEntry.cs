namespace Ready4Balfolk.Domain.Models.History;

public sealed record MessageHistoryEntry(
    string Message,
    TimeSpan? Duration,
    CompletionStatus CompletionStatus) : QueueHistoryEntry(CompletionStatus);
