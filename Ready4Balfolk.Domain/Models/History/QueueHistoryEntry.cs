using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.History;

/// <summary>One thing that happened in a night, between a start and a finish.</summary>
/// <remarks>
/// <para>
/// Every stored member, here and on each kind of entry, names itself with <c>[JsonPropertyName]</c>
/// in the spelling it has always been written under. A night in the database is the only copy there
/// is of an evening, and a property renamed without its stored name would read every older night
/// back with that member empty: a track with no path, say, when the path is what the duplicate rule
/// recognises a played track by. A test holds the names.
/// </para>
/// <para>
/// Both times are nullable and defaulted so history written before they existed still deserialises;
/// those entries simply have no time to show. A finish is what makes the list say how long a thing
/// really ran and when it gave way to the next: a duration on its own is how long a track is, not
/// how long it was heard for.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TrackHistoryEntry), "track")]
[JsonDerivedType(typeof(MessageHistoryEntry), "message")]
[JsonDerivedType(typeof(DelayHistoryEntry), "delay")]
[JsonDerivedType(typeof(StopHistoryEntry), "stop")]
[JsonDerivedType(typeof(EndOfNightHistoryEntry), "endOfNight")]
public abstract record QueueHistoryEntry(
    [property: JsonPropertyName("CompletionStatus")] CompletionStatus CompletionStatus,
    [property: JsonPropertyName("StartedAt")] DateTime? StartedAt = null,
    [property: JsonPropertyName("FinishedAt")] DateTime? FinishedAt = null);
