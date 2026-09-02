using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The evening itself: what goes in the queue, what plays, and what is left behind.</summary>
public sealed class RunningTheEvening(HeadlessSession session)
{
    /// <summary>The DJ plays a dance the room asked for, and it is in the night's account.</summary>
    /// <remarks>
    /// World: a library of two tracks, and auto queue off, so nothing goes into the queue that the
    /// DJ did not put there.
    /// Steps: find the track in the catalogue, put it in the queue, press next to start the
    /// evening, and let the track play out.
    /// Sees: the track in the queue, then playing, and then in the history with the time it ran and
    /// the fact that it finished rather than being cut short.
    /// </remarks>
    [Fact]
    public async Task DjQueuesADanceAndPlaysItThrough()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            Assert.Contains(
                application.RowsOf("queue.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal));

            // Next, not play: with nothing playing yet, this is the button that starts the
            // evening, and it is labelled for that.
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.title").Contains("Salamandre", StringComparison.Ordinal),
                "the track to start playing");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the track to play out");

            application.Click("queue.show-history");

            Assert.Contains(
                application.RowsOf("history.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal)
                       && row.Contains(UiStrings.History_StatusFinished, StringComparison.Ordinal));
        });
    }

    /// <summary>The DJ cuts a dance short because the room has had enough of it.</summary>
    /// <remarks>
    /// World: a library of two tracks and auto queue off.
    /// Steps: queue both, start the first, and press next part way through, confirming when asked.
    /// Sees: the second track playing, and the first one in the history as skipped rather than as
    /// something the room danced to the end of.
    /// </remarks>
    [Fact]
    public async Task DjSkipsATrackPartWayThrough()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.title").Contains("Salamandre", StringComparison.Ordinal),
                "the first track to start playing");

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to skip");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.TextOf("playback.title").Contains("La Belle", StringComparison.Ordinal),
                "the next track to take over");

            application.Click("queue.show-history");

            Assert.Contains(
                application.RowsOf("history.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal)
                       && row.Contains(UiStrings.History_StatusSkipped, StringComparison.Ordinal));
        });
    }

    /// <summary>The DJ empties a queue they built for the wrong part of the evening.</summary>
    /// <remarks>
    /// World: a library of two tracks and auto queue off.
    /// Steps: queue both, then clear the queue and agree to it.
    /// Sees: an empty queue, and nothing in the history, because nothing was ever played.
    /// </remarks>
    [Fact]
    public async Task DjClearsTheQueueAndConfirmsIt()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            Assert.Equal(2, application.RowsOf("queue.items").Count);

            application.Click("queue.clear");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to clear the queue");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 0,
                "the queue to empty");

            application.Click("queue.show-history");

            Assert.Empty(application.RowsOf("history.items"));
        });
    }

    /// <summary>The DJ holds the music while somebody says something to the room.</summary>
    /// <remarks>
    /// World: a library of one track and auto queue off.
    /// Steps: queue it, start it, pause it, wait, and start it again.
    /// Sees: the progress bar stop where it was while the music is held, and carry on from there
    /// rather than from the beginning.
    /// </remarks>
    [Fact]
    public async Task DjPausesAndPicksUpWhereTheyLeftOff()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > 0,
                "the track to get under way");

            application.Click("playback.play-pause");
            var heldAt = application.ProgressOf("playback.progress");

            await Task.Delay(300);
            application.Settle();

            Assert.Equal(heldAt, application.ProgressOf("playback.progress"));

            application.Click("playback.play-pause");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > heldAt,
                "the music to pick up where it was");
        });
    }

    /// <summary>The DJ takes a dance back out of the queue before the room gets to it.</summary>
    /// <remarks>
    /// World: a library of two tracks and auto queue off.
    /// Steps: queue both, pick the one that is no longer wanted, and remove it.
    /// Sees: a queue with only the other one left in it.
    /// </remarks>
    [Fact]
    public async Task DjTakesADanceBackOutOfTheQueue()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            application.Click(application.Row("queue.items", "La Belle"));
            application.Click("queue.remove");

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the queue to lose the dance that was taken out");

            Assert.Contains(
                application.RowsOf("queue.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal));
        });
    }
}
