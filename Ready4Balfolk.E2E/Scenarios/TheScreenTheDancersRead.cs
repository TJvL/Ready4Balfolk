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
                () => application.TextOf("display.title").Contains("Salamandre", StringComparison.Ordinal),
                "the screen to show what is playing");

            Assert.Contains("Mazurka", application.TextOf("display.dance"), StringComparison.Ordinal);
            Assert.Contains("La Belle", application.TextOf("display.next-title"), StringComparison.Ordinal);
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
            Assert.False(application.IsShowing("display.title"), "A track name was left on the screen.");
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
