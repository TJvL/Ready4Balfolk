using System.Globalization;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The end of the night for the DJ rather than for the room.</summary>
public sealed class ClosingUp(HeadlessSession session)
{
    /// <summary>The DJ closes the application and finds their window where they left it.</summary>
    /// <remarks>
    /// World: a library of one dance, and a window the size the DJ last had it.
    /// Steps: press exit and agree to it.
    /// Sees: the window gone, and the size and position it had written down for the next evening.
    /// </remarks>
    [Fact]
    public async Task DjClosesTheAppAndFindsTheirWindowWhereTheyLeftIt()
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

            application.Click("toolbar.exit");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to close");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => !application.Window.IsVisible,
                "the window to close");

            var saved = world.SettingsOnDisk().MainWindowState;

            Assert.Equal(1600, saved.Width);
            Assert.Equal(1000, saved.Height);
        });
    }

    /// <summary>The DJ changes their mind about closing, mid evening.</summary>
    /// <remarks>
    /// World: a library of one dance, and auto queue off.
    /// Steps: queue a dance, start it, press exit, and decline.
    /// Sees: the window still there and the music still going, which is the whole reason the
    /// application asks.
    /// </remarks>
    [Fact]
    public async Task DjChangesTheirMindAboutClosing()
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
                "the dance to get under way");

            application.Click("toolbar.exit");

            await application.WaitUntil(
                () => application.IsShowing("dialog.cancel"),
                "the application to ask whether to close");

            var playedTo = application.ProgressOf("playback.progress");
            application.Click("dialog.cancel");

            Assert.True(application.Window.IsVisible, "The window closed after the DJ said not to.");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > playedTo,
                "the music to still be running");
        });
    }

    /// <summary>The DJ is asked about an evening that was never ended.</summary>
    /// <remarks>
    /// World: a machine with a night on it that nobody closed, from nine hours ago, which is what a
    /// flat laptop or a lid shut at three in the morning leaves behind.
    /// Steps: open the application.
    /// Sees: being asked once whether that evening is over, at the one moment the question does not
    /// interrupt a room.
    /// </remarks>
    [Fact]
    public async Task DjIsAskedAboutTheUnfinishedNight()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithAnEveningNobodyEnded(TimeSpan.FromHours(9))
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask about the evening that was never ended");

            application.Click("dialog.confirm");
            application.Click("queue.show-history");

            // Filed at the moment the music stopped rather than at the moment somebody was asked
            // about it. The question comes at the next start, which can be days later, and an
            // evening that reads as having run until Tuesday is not the evening anybody had.
            var stopped = world.LastDanceEndedAt.ToString("HH:mm", CultureInfo.CurrentCulture);

            await application.WaitUntil(
                () => application.SeesAnywhere(string.Format(
                    CultureInfo.CurrentCulture, UiStrings.History_NightEnded, stopped)),
                "the night to be filed at the time it actually stopped");
        });
    }
}
