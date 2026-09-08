using System.Globalization;
using Avalonia;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The end of the night for the DJ rather than for the room.</summary>
public sealed class ClosingUp(HeadlessSession session)
{
    /// <summary>The DJ closes the application and finds their window where they left it.</summary>
    /// <remarks>
    /// World: a library of one dance, and a window the size the DJ last had it.
    /// Steps: move and resize the window, then press exit and agree to it.
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

            // A window nobody seeded: the world laid down 1600 by 1000 at the corner, so a run
            // that asserted that back would be green whether or not closing ever read the window.
            application.TheDjMovesAndResizesTheWindow(new PixelPoint(140, 70), 1280, 820);

            application.Click("toolbar.exit");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to close");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => !application.Window.IsVisible,
                "the window to close");

            var saved = world.SettingsOnDisk().MainWindowState;

            Assert.Equal(1280, saved.Width);
            Assert.Equal(820, saved.Height);
            Assert.Equal(140, saved.X);
            Assert.Equal(70, saved.Y);
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
    /// interrupt a room, in a window the size of the question rather than a fixed one the longer
    /// half of the question falls out of.
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

            // The window is the length of what it is asking rather than a number somebody typed
            // into the view, and nothing inside it is scrolling, which together are what keeps the
            // question readable whatever it says: this one names a date and a count, and the Dutch
            // of it runs longer than the English.
            Assert.True(
                application.TheWindowShowingItShowsTheWholeOfWhatItHolds("dialog.confirm"),
                "Part of the question is out of sight in a window that is not the size of it.");

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
