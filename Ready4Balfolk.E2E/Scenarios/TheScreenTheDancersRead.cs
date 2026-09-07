using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The presentation window, which is the only part of this a room ever sees.</summary>
public sealed class TheScreenTheDancersRead(HeadlessSession session)
{
    /// <summary>The dancers see what is playing and what is coming.</summary>
    /// <remarks>
    /// World: a library of two dances, a screen switched on for the room, and auto queue off.
    /// Steps: queue both dances and start the evening.
    /// Sees: the dance being danced, and the one behind it, on the screen rather than on the
    /// desktop the DJ is looking at.
    /// </remarks>
    [Fact]
    public async Task DancersSeeWhatIsPlayingAndWhatIsNext()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
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
                () => application.TextOf("display.track").Contains("Salamandre", StringComparison.Ordinal),
                "the screen to show what is playing");

            Assert.Contains("Mazurka", application.TextOf("display.dance"), StringComparison.Ordinal);
            Assert.Contains("La Belle", application.TextOf("display.next-track"), StringComparison.Ordinal);
        });
    }

    /// <summary>The room is told what the pause is for, on both screens.</summary>
    /// <remarks>
    /// World: a library of two dances, a screen switched on for the room, the server on for the
    /// hall's browser, and auto queue off.
    /// Steps: start one dance, put a delay behind it, and a second dance behind the delay, which is
    /// what a DJ does to give the room time to make lines.
    /// Sees: both screens showing the delay as what is next and the dance waiting behind it, in one
    /// run. During that pause the one thing the floor wants to know is what it is getting ready
    /// for, and that is exactly when a screen showing only the pause stops answering.
    /// </remarks>
    [Fact]
    public async Task DancersSeeTheDanceWaitingBehindTheDelay()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await using var projector = await TheBrowser.OpenAt(world.ServerAddress);

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("display.track").Contains("Salamandre", StringComparison.Ordinal),
                "the screen to show what is playing");

            application.Click("queue.delay");
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            await application.WaitUntil(
                () => application.IsShowing("display.behind-dance"),
                "the screen to name the dance the pause is for");

            // The name the shared list carries, which is what every screen says: the tag says
            // Schottische and the vocabulary calls it Scottish.
            Assert.Equal(UiStrings.Presentation_Delay, application.TextOf("display.next-dance"));
            Assert.Equal("Scottish", application.TextOf("display.behind-dance"));
            Assert.Equal("Trio Loubelya - La Belle", application.TextOf("display.behind-track"));

            // The same picture in the hall's browser, which is the other screen a room reads. It
            // writes the artist and the title into boxes of their own rather than as one line, so
            // what is read back below is the title on its own.
            await projector.WaitUntilItReads("behindPrimary", "Scottish");

            Assert.Equal("La Belle", await projector.Reads("behindTitle"));
        });
    }

    /// <summary>A long name stays on the screen instead of running off both edges of it.</summary>
    /// <remarks>
    /// World: one dance whose artist and title are as long as a real library makes them, and a
    /// screen switched on for the room.
    /// Steps: queue it and start the evening.
    /// Sees: the whole line inside the window, wrapped onto as many lines as it takes. The hall
    /// reads this from the far wall and nobody standing there can scroll it sideways.
    /// </remarks>
    [Fact]
    public async Task ALongNameStaysOnTheScreen()
    {
        const string artist = "Naragonia Quartet en Ambrozijn";
        const string title = "Salamandre, live op het Boombalfestival";

        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: artist, title: title)
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", title));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("display.track").Contains(title, StringComparison.Ordinal),
                "the screen to show what is playing");

            var line = application.Find("display.track");

            Assert.True(
                RunningApplication.WordsFitWhereTheyAreDrawn(line),
                $"The track ran off the screen the dancers read: {RunningApplication.Says(line)} "
                + $"was drawn in {line.Bounds}.");
        });
    }

    /// <summary>The screen says nothing is playing rather than the last thing that did.</summary>
    /// <remarks>
    /// World: a library of one dance and a screen switched on for the room.
    /// Steps: start the application and leave the queue alone.
    /// Sees: the screen saying the floor is between dances, and no track name left on it.
    /// </remarks>
    [Fact]
    public async Task TheScreenGoesIdleWhenNothingIsPlaying()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("display.idle"),
                "the screen to say that nothing is playing");

            Assert.Equal(UiStrings.Presentation_NoTrackPlaying, application.TextOf("display.idle"));
            Assert.False(application.IsShowing("display.track"), "A track name was left on the screen.");
        });
    }

    /// <summary>The DJ puts a second screen up for the other room.</summary>
    /// <remarks>
    /// World: a library of one dance, and one screen already on.
    /// Steps: open the settings and ask for a second screen.
    /// Sees: two screens up for the room instead of one.
    /// </remarks>
    [Fact]
    public async Task DjOpensASecondScreenForTheOtherRoom()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.ScreensShowing() == 1,
                "the screen the DJ already had");

            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.screens"),
                "the settings to come up");

            application.TypeInto("settings.screens", "2");

            await application.WaitUntil(
                () => application.ScreensShowing() == 2,
                "the second screen to come up");
        });
    }

    /// <summary>The DJ takes the screen down again part way through the evening.</summary>
    /// <remarks>
    /// World: a library of one dance, and a screen up for the room.
    /// Steps: open the settings and ask for no screens.
    /// Sees: the screen gone, without the application needing a restart.
    /// </remarks>
    [Fact]
    public async Task DjTurnsTheScreenOffAgainMidEvening()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                PresentationDisplayCount = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.ScreensShowing() == 1,
                "the screen the DJ had up");

            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.screens"),
                "the settings to come up");

            application.TypeInto("settings.screens", "0");

            await application.WaitUntil(
                () => application.ScreensShowing() == 0,
                "the screen to come down");
        });
    }
}
