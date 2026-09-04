using System.Text.RegularExpressions;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The account of an evening, during it and after it.</summary>
public sealed partial class LookingBackAtTheNight(HeadlessSession session)
{
    /// <summary>The clock times a row carries: when a thing started and when it stopped.</summary>
    [GeneratedRegex(@"\d{2}:\d{2}")]
    private static partial Regex ClockTimes();

    /// <summary>The DJ reads back what the evening has been so far.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off, so nothing is in the night that the DJ did
    /// not put there.
    /// Steps: play a dance through, put a delay and a message behind it and move past them, then
    /// open the history.
    /// Sees: the night marked where it began, every kind of item in it in the order it happened,
    /// and a start and a finish on each. A duration on its own is how long a track is; what a room
    /// heard is the time between those two.
    /// </remarks>
    [Fact]
    public async Task DjLooksBackOverTheEvening()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the dance to play out");

            application.Click("queue.delay");
            application.Click("playback.skip");

            application.Click("queue.message");
            application.TypeInto("message.text", "Last dance in ten minutes");
            application.Click("message.ok");
            application.Click("playback.skip");
            application.Click("playback.skip");

            application.Click("queue.show-history");

            await application.WaitUntil(
                () => application.RowsOf("history.items").Count >= 4,
                "the night to hold everything that happened in it");

            var rows = application.RowsOf("history.items");

            Assert.Contains(UiStrings.History_NightStarted.Split('{')[0].Trim(), rows[0], StringComparison.Ordinal);

            // In the order they happened, and each one saying when it started and when it stopped.
            Assert.Contains("Salamandre", rows[1], StringComparison.Ordinal);
            Assert.Contains(UiStrings.History_TypeDelay, rows[2], StringComparison.Ordinal);
            Assert.Contains("Last dance in ten minutes", rows[3], StringComparison.Ordinal);

            foreach (var row in rows.Skip(1))
            {
                Assert.Equal(2, ClockTimes().Count(row));
            }
        });
    }

    /// <summary>The evening is over, and the DJ can still read what was played.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off.
    /// Steps: play a dance, then start a new night the way a DJ does after a soundcheck, and look at
    /// the history.
    /// Sees: the evening still on screen, named as the night it was rather than gone, with the line
    /// that says when it was called. Tonight is one click away and empty, which is the truth about
    /// tonight.
    /// </remarks>
    [Fact]
    public async Task DjLooksBackAtAnEveningThatHasAlreadyEnded()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the dance to play out");

            application.Click("queue.show-history");
            application.Click("history.new-night");
            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.SeesAnywhere(UiStrings.History_NightEnded.Split('{')[0].Trim()),
                "the evening to stay on screen as a night that has ended");

            Assert.Contains(
                application.RowsOf("history.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal));

            // Tonight is the other thing this screen can show, and tonight nothing has happened.
            application.Click("history.nights");
            application.Click(application.Offering(UiStrings.History_Tonight));

            await application.WaitUntil(
                () => application.RowsOf("history.items").Count == 0,
                "tonight to be empty, because tonight nothing has happened yet");
        });
    }

    /// <summary>The closing track plays, and the night is in the history with both ends on it.</summary>
    /// <remarks>
    /// World: a library with one dance and a file nominated as the sound of the evening ending.
    /// Steps: queue the closing track, let it play out, and open the history.
    /// Sees: the night filed by the closing song itself, with the line that says when it began and
    /// the line that says when it ended, and the closing track between them. Nobody has to remember
    /// to press anything while packing up.
    /// </remarks>
    [Fact]
    public async Task DjEndsTheNightWithTheClosingTrack()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithEndOfNightAudio()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("queue.end-of-night");
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.dance").Contains(UiStrings.Playback_EndOfNight, StringComparison.Ordinal),
                "the closing track to start");

            application.Click("queue.show-history");

            // The closing song playing out is what files the night, without anybody pressing
            // anything: waiting for the line that says so is waiting for the evening to be over.
            await application.WaitUntil(
                () => application.SeesAnywhere(UiStrings.History_NightEnded.Split('{')[0].Trim()),
                "the night to be filed with an end on it");

            var rows = application.RowsOf("history.items");

            Assert.Contains(UiStrings.History_NightStarted.Split('{')[0].Trim(), rows[0], StringComparison.Ordinal);
            Assert.Contains(UiStrings.History_TypeEndOfNight, rows[1], StringComparison.Ordinal);
            Assert.Contains(UiStrings.History_NightEnded.Split('{')[0].Trim(), rows[^1], StringComparison.Ordinal);
        });
    }

    /// <summary>The organisers ask for the evening afterwards, and it is still there to give.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off.
    /// Steps: play a dance, file the night, and export what is on screen.
    /// Sees: a file holding the evening that ended, not the empty one that is running. An export
    /// that could only ever write tonight is no use the morning after.
    /// </remarks>
    [Fact]
    public async Task DjExportsTheNightForTheOrganisers()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        var export = Path.Combine(world.DirectoryInfoRoot.FullName, "for the organisers.json");

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the dance to play out");

            application.Click("queue.show-history");
            application.Click("history.new-night");
            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.SeesAnywhere(UiStrings.History_NightEnded.Split('{')[0].Trim()),
                "the night to be filed");

            RunningApplication.TheDjWillPick(export);
            application.Click("history.export");

            await application.WaitUntil(
                () => File.Exists(export) && File.ReadAllText(export).Contains("Salamandre", StringComparison.Ordinal),
                "the evening that ended to be written out");
        });
    }

    /// <summary>The soundcheck is thrown away, and the evening it was not part of is left alone.</summary>
    /// <remarks>
    /// World: a library of two dances and auto queue off.
    /// Steps: play one dance while testing the speakers, start a new night, play the other, and
    /// then go back and delete the soundcheck.
    /// Sees: the soundcheck gone and tonight untouched. Without this the file grew for the life of
    /// the application with nothing anybody could do about it.
    /// </remarks>
    [Fact]
    public async Task DjThrowsAwayTheNightTheyWereTesting()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the soundcheck to play out");

            application.Click("queue.show-history");
            application.Click("history.new-night");
            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.SeesAnywhere(UiStrings.History_NightEnded.Split('{')[0].Trim()),
                "the soundcheck to be filed");

            // The evening proper, which must survive the tidying up.
            application.Click("history.show-queue");
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.title"),
                "the dance to play out");

            application.Click("queue.show-history");
            application.Click("history.delete-night");
            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.RowsOf("history.items").Count > 0
                      && application.RowsOf("history.items").Any(row =>
                          row.Contains("La Belle", StringComparison.Ordinal)),
                "tonight to be what is left");

            Assert.DoesNotContain(
                application.RowsOf("history.items"),
                row => row.Contains("Salamandre", StringComparison.Ordinal));
        });
    }

    /// <summary>The night says a file was missing rather than saying the DJ skipped it.</summary>
    /// <remarks>
    /// World: a library with one dance and a file nominated as the sound of the evening ending,
    /// which lives outside the music directory and so is watched by nothing.
    /// Steps: queue the closing track, take its file away, reach it, and read the night back.
    /// Sees: the entry recorded as a file that was not there. A DJ reading the night back needs to
    /// tell a decision from a file that had gone: one of them is theirs to explain.
    /// </remarks>
    [Fact]
    public async Task DjLooksBackAtATrackThatCouldNotBePlayed()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithEndOfNightAudio()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("queue.end-of-night");
            world.RemoveTheEndOfNightFile();

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say that the closing track could not be played");

            application.Click("queue.show-history");

            await application.WaitUntil(
                () => application.RowsOf("history.items").Any(row =>
                    row.Contains(UiStrings.History_StatusFileMissing, StringComparison.Ordinal)),
                "the night to say the file was missing");

            Assert.DoesNotContain(
                application.RowsOf("history.items"),
                row => row.Contains(UiStrings.History_StatusSkipped, StringComparison.Ordinal));
        });
    }
}
