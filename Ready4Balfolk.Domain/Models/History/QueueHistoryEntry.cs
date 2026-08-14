using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TrackHistoryEntry), "track")]
[JsonDerivedType(typeof(MessageHistoryEntry), "message")]
[JsonDerivedType(typeof(DelayHistoryEntry), "delay")]
[JsonDerivedType(typeof(StopHistoryEntry), "stop")]
[JsonDerivedType(typeof(EndOfNightHistoryEntry), "endOfNight")]
// StartedAt is nullable and defaulted so history written before it existed still deserialises;
// those entries simply have no start time to show.
public abstract record QueueHistoryEntry(CompletionStatus CompletionStatus, DateTime? StartedAt = null);
