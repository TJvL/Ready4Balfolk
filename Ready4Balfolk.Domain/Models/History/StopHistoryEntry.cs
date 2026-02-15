namespace Ready4Balfolk.Domain.Models.History;

public sealed record StopHistoryEntry(
    CompletionStatus CompletionStatus) : QueueHistoryEntry(CompletionStatus);
