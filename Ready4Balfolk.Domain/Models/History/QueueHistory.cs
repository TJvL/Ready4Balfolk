using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

/// <summary>One night: what happened between a start and an end.</summary>
/// <remarks>
/// A night begins with the first thing recorded after the last one ended, so <see cref="StartedAt"/>
/// is set by the first entry rather than by anybody pressing anything, and <see cref="EndedAt"/> is
/// what makes it a night that finished rather than the one still running.
/// </remarks>
public sealed record QueueHistory(
    DateTime? StartedAt,
    List<QueueHistoryEntry> Entries)
{
    public static QueueHistory Empty { get; } = new(null, []);

    /// <summary>Which night this is, or zero for one nothing has happened in yet.</summary>
    /// <remarks>
    /// Left out of an export: an export is an evening to hand to somebody, not a row out of
    /// somebody else's database.
    /// </remarks>
    [JsonIgnore]
    public long Id { get; init; }

    /// <summary>When the evening was called, or nothing while it is still the current one.</summary>
    public DateTime? EndedAt { get; init; }

    [JsonIgnore]
    public bool IsOpen => EndedAt is null;

    /// <summary>
    /// When something last happened, which is how a night nobody ended is judged stale, and when
    /// such a night is taken to have ended.
    /// </summary>
    /// <remarks>
    /// The finish of the last thing in it rather than its start: an evening ends when the music
    /// does, not when the last track began.
    /// </remarks>
    [JsonIgnore]
    public DateTime? LastActivityAt => Entries.Count > 0
        ? Entries[^1].FinishedAt ?? Entries[^1].StartedAt ?? StartedAt
        : StartedAt;
}
