using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TrackHistoryEntry), "track")]
[JsonDerivedType(typeof(MessageHistoryEntry), "message")]
[JsonDerivedType(typeof(DelayHistoryEntry), "delay")]
[JsonDerivedType(typeof(StopHistoryEntry), "stop")]
public abstract record QueueHistoryEntry(CompletionStatus CompletionStatus);
