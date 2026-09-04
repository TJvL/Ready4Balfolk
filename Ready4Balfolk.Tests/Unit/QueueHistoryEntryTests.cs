using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.History;

namespace Ready4Balfolk.Tests.Unit;

public sealed class QueueHistoryEntryTests
{
    // Mirrors QueueHistoryStore: enums are written as strings on disk.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    [Fact]
    public void Deserialize_EntryWithoutStartedAt_IsNull()
    {
        // History written before start times were recorded.
        const string json = """
            {
              "type": "track",
              "FilePath": "/music/a.mp3",
              "Dance": "Mazurka",
              "Artist": "Artist",
              "Title": "Title",
              "Duration": "00:03:00",
              "RandomlyAdded": false,
              "CompletionStatus": "Finished"
            }
            """;

        var entry = JsonSerializer.Deserialize<QueueHistoryEntry>(json, JsonOptions);

        var track = Assert.IsType<TrackHistoryEntry>(entry);
        Assert.Null(track.StartedAt);
    }

    [Fact]
    public void Serialize_RoundTripsStartedAt()
    {
        var startedAt = new DateTime(2026, 8, 6, 21, 14, 0, DateTimeKind.Local);
        QueueHistoryEntry entry = new TrackHistoryEntry(
            "/music/a.mp3", "Mazurka", "Artist", "Title", TimeSpan.FromMinutes(3), false,
            CompletionStatus.Finished, startedAt);

        var round = JsonSerializer.Deserialize<QueueHistoryEntry>(JsonSerializer.Serialize(entry, JsonOptions), JsonOptions);

        Assert.Equal(startedAt, Assert.IsType<TrackHistoryEntry>(round).StartedAt);
    }

    [Fact]
    public void Stop_And_Delay_EntriesAlsoCarryStartedAt()
    {
        var startedAt = new DateTime(2026, 8, 6, 21, 14, 0, DateTimeKind.Local);

        Assert.Equal(startedAt, new StopHistoryEntry(CompletionStatus.Skipped, startedAt).StartedAt);
        Assert.Equal(startedAt,
            new DelayHistoryEntry(TimeSpan.FromSeconds(30), CompletionStatus.Finished, startedAt).StartedAt);
    }

    [Fact]
    public void EndOfNight_RoundTrips()
    {
        var startedAt = new DateTime(2026, 8, 6, 23, 58, 0, DateTimeKind.Local);
        QueueHistoryEntry entry = new EndOfNightHistoryEntry(
            TimeSpan.FromMinutes(4), CompletionStatus.Finished, startedAt);

        var round = JsonSerializer.Deserialize<QueueHistoryEntry>(
            JsonSerializer.Serialize(entry, JsonOptions), JsonOptions);

        var endOfNight = Assert.IsType<EndOfNightHistoryEntry>(round);
        Assert.Equal(startedAt, endOfNight.StartedAt);
        Assert.Equal(TimeSpan.FromMinutes(4), endOfNight.Duration);
    }

    [Fact]
    public void ANight_LastHappenedWhenItsLastEntryFinished()
    {
        // The finish rather than the start: an evening ends when the music does, and this is both
        // how a night is judged stale and when it is taken to have ended.
        var started = new DateTime(2026, 7, 12, 23, 50, 0, DateTimeKind.Local);
        var history = new QueueHistory(started,
        [
            new TrackHistoryEntry("/music/a.mp3", "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished,
                started, started.AddMinutes(3))
        ]);

        Assert.Equal(started.AddMinutes(3), history.LastActivityAt);
    }

    [Fact]
    public void AnEntry_RemembersWhenItStartedAndWhenItStopped()
    {
        var startedAt = new DateTime(2026, 7, 12, 21, 4, 0, DateTimeKind.Local);
        QueueHistoryEntry entry = new TrackHistoryEntry("/music/a.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Skipped, startedAt,
            startedAt + TimeSpan.FromSeconds(40));

        var round = JsonSerializer.Deserialize<QueueHistoryEntry>(
            JsonSerializer.Serialize(entry, JsonOptions), JsonOptions);

        var track = Assert.IsType<TrackHistoryEntry>(round);
        Assert.Equal(startedAt, track.StartedAt);

        // Forty seconds of a three minute track, which is what the room actually heard.
        Assert.Equal(startedAt + TimeSpan.FromSeconds(40), track.FinishedAt);
    }
}
