using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

public sealed record QueueHistory(
    DateTime? StartedAt,
    List<QueueHistoryEntry> Entries)
{
    public static QueueHistory Empty { get; } = new(null, []);

    [JsonIgnore]
    public TimeSpan TotalDuration => Entries.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry switch
    {
        TrackHistoryEntry t => t.Duration,
        MessageHistoryEntry m => m.Duration ?? TimeSpan.Zero,
        DelayHistoryEntry d => d.Duration,
        EndOfNightHistoryEntry e => e.Duration ?? TimeSpan.Zero,
        _ => TimeSpan.Zero
    });
}
