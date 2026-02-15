namespace Ready4Balfolk.Domain.Models.History;

public sealed record DelayHistoryEntry(
    TimeSpan Duration,
    CompletionStatus CompletionStatus) : QueueHistoryEntry(CompletionStatus);
