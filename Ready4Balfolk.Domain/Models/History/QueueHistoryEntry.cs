using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

/// <summary>One thing that happened in a night, between a start and a finish.</summary>
/// <remarks>
/// Both times are nullable and defaulted so history written before they existed still deserialises;
/// those entries simply have no time to show. A finish is what makes the list say how long a thing
/// really ran and when it gave way to the next: a duration on its own is how long a track is, not
/// how long it was heard for.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TrackHistoryEntry), "track")]
[JsonDerivedType(typeof(MessageHistoryEntry), "message")]
[JsonDerivedType(typeof(DelayHistoryEntry), "delay")]
[JsonDerivedType(typeof(StopHistoryEntry), "stop")]
[JsonDerivedType(typeof(EndOfNightHistoryEntry), "endOfNight")]
public abstract record QueueHistoryEntry(
    CompletionStatus CompletionStatus,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null);
