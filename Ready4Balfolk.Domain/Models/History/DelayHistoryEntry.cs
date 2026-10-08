using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

public sealed record DelayHistoryEntry(
    [property: JsonPropertyName("Duration")] TimeSpan Duration,
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null) : QueueHistoryEntry(CompletionStatus, StartedAt, FinishedAt);
