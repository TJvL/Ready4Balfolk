using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>Winding the evening down, and what the application will not do once it is over.</summary>
public sealed class EndingTheEvening(HeadlessSession session)
{
    /// <summary>The DJ closes the evening without having to touch the queue at the end of it.</summary>
    /// <remarks>
    /// World: a library with a dance, and a file nominated as the sound of the evening ending.
    /// Steps: queue a dance, put the closing track behind it, start the evening, and let it run.
    /// Sees: the closing track queued behind the dance, and playing when it gets there.
    /// What history holds afterwards is asserted in #106, where that behaviour changes.
    /// </remarks>
    [Fact]
    public async Task DjQueuesTheClosingTrackAndItPlaysLast()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithEndOfNightAudio()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("queue.end-of-night");

            Assert.Equal(2, application.RowsOf("queue.items").Count);

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the last dance to start");

            await application.WaitUntil(
                () => application.TextOf("playback.dance").Contains(UiStrings.Playback_EndOfNight, StringComparison.Ordinal),
                "the closing track to take over when the last dance is done");
        });
    }

    /// <summary>Nothing else goes in once the DJ has declared the evening over.</summary>
    /// <remarks>
    /// World: a library with two dances, and a file nominated as the sound of the evening ending.
    /// Steps: put the closing track in an empty queue, start it, and then try to queue a dance.
    /// Sees: the dance refused, with a message saying the evening is over, and a queue that has not
    /// grown behind the closing track.
    /// </remarks>
    [Fact]
    public async Task NothingElseIsAcceptedAfterTheNightHasEnded()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WithEndOfNightAudio()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.Click("queue.end-of-night");
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.dance").Contains(UiStrings.Playback_EndOfNight, StringComparison.Ordinal),
                "the closing track to start");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say that the evening is over");

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }

    /// <summary>The DJ nominated a closing track, and the file is no longer where it was.</summary>
    /// <remarks>
    /// World: a library with a dance, and a settings file pointing at a closing track that is not
    /// there, which is what a moved or renamed file leaves behind.
    /// Steps: look at the queue toolbar.
    /// Sees: the closing track cannot be queued, rather than being queued and failing at the moment
    /// the room is waiting for it.
    /// </remarks>
    [Fact]
    public async Task TheEndOfTheNightFileHasBeenMovedAway()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheEndOfNightFileHasBeenMovedAway()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            Assert.False(
                application.Find("queue.end-of-night").IsEffectivelyEnabled,
                "The closing track was on offer, and the file it plays is not there.");
        });
    }
}
