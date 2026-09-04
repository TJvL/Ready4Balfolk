using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The evening itself: what goes in the queue, what plays, and what is left behind.</summary>
public sealed class RunningTheEvening(HeadlessSession session)
{
    /// <summary>The room gets a moment between one dance and the next.</summary>
    /// <remarks>
    /// World: a library of two dances, auto queue off, and the standard gap switched on at two
    /// seconds.
    /// Steps: queue both dances and start the evening.
    /// Sees: the first dance play out, a moment where nothing is playing while the second is still
    /// in the queue, and then the second starting. A floor clears and re-forms in that moment, and
    /// the DJ did not have to queue a delay to get it.
    /// </remarks>
    [Fact]
    public async Task TheRoomGetsAMomentBetweenDances()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false,
                GapBetweenTracksEnabled = true,
                GapBetweenTracksSeconds = 2
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
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start");

            // The gap itself: the floor is told what is happening rather than left reading a bar
            // that says nothing, and the second dance is still in the queue rather than taken out
            // of it.
            await application.WaitUntil(
                () => application.TextOf("playback.dance")
                          .Contains(UiStrings.Playback_Gap, StringComparison.Ordinal)
                      && application.RowsOf("queue.items").Any(row =>
                          row.Contains("La Belle", StringComparison.Ordinal)),
                "the floor to be given its moment");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the second dance to start after the moment has passed");
        });
    }

    /// <summary>The gap is nowhere: not in the queue, not on the screen, not in the night.</summary>
    /// <remarks>
    /// World: a library of two dances, a screen for the room, and the gap switched on.
    /// Steps: play both dances through, then read the queue, the screen and the history.
    /// Sees: two rows in the queue and two entries in the night, with nothing about the quiet in
    /// between. A queue with a delay between every pair of tracks is a queue nobody can read, and a
    /// night's account of ten seconds of quiet is not what was played or what was decided.
    /// </remarks>
    [Fact]
    public async Task TheGapIsNotInTheQueueOrTheNight()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false,
                PresentationDisplayCount = 1,
                GapBetweenTracksEnabled = true,
                GapBetweenTracksSeconds = 1
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            // Two rows before the evening starts, and two rows are all there will ever be.
            Assert.Equal(2, application.RowsOf("queue.items").Count);

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start");

            // The screen in the hall names the moment and keeps naming the dance behind it, rather
            // than saying nothing is playing while the music is only pausing.
            await application.WaitUntil(
                () => application.TextOf("display.dance")
                    .Contains(UiStrings.Presentation_Gap, StringComparison.Ordinal),
                "the screen to say what the moment is");

            Assert.Contains("Scottish", application.TextOf("display.next-dance"), StringComparison.Ordinal);

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the second dance to start");

            await application.WaitUntil(
                () => !application.IsShowing("playback.track"),
                "the evening to run out");

            application.Click("queue.show-history");

            await application.WaitUntil(
                () => application.RowsOf("history.items").Count(row =>
                    row.Contains(UiStrings.History_StatusFinished, StringComparison.Ordinal)) == 2,
                "the night to hold the two dances");

            Assert.DoesNotContain(
                application.RowsOf("history.items"),
                row => row.Contains(UiStrings.History_TypeDelay, StringComparison.Ordinal));
        });
    }

    /// <summary>A clicked seek bar asks once, however many times it is clicked.</summary>
    /// <remarks>
    /// World: a library of one dance, auto queue off, and the confirmations a DJ is asked for left
    /// on.
    /// Steps: start the track, click the seek bar, and click it again while the confirmation is
    /// still up, the way a hand does when the first click looks like it did nothing.
    /// Sees: one confirmation, and nothing left behind after answering it. A second confirmation
    /// waiting behind the first is a track being walked forward a jump at a time in front of a
    /// room.
    /// </remarks>
    [Fact]
    public async Task ClickingTheSeekBarTwiceAsksOnce()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = true
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
                () => application.ProgressOf("playback.progress") > 0,
                "the dance to get under way");

            application.Click("playback.progress");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the DJ to be asked about moving the track");

            // The same click again, which is what a hand does when the first one looks like it did
            // nothing. It must not start a second seek of its own.
            application.Click("playback.progress");

            Assert.Single(application.Window.OwnedWindows);

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => !application.IsShowing("dialog.confirm"),
                "the question to be answered and gone");

            Assert.Empty(application.Window.OwnedWindows);
        });
    }

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
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the track to start playing");

            await application.WaitUntil(
                () => !application.IsShowing("playback.track"),
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
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first track to start playing");

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to skip");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
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

    /// <summary>The DJ gives the room a moment to make lines before the next dance.</summary>
    /// <remarks>
    /// World: a library of two tracks, auto queue off, and a delay of a second, which is the length
    /// this DJ has set for the pauses they announce.
    /// Steps: queue a dance, queue a delay, queue the dance that follows it, and start the evening.
    /// Sees: the delay counted down in its turn, and the dance behind it taking over on its own.
    /// </remarks>
    [Fact]
    public async Task DjQueuesADelaySoTheRoomCanFormLines()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false, DelaySeconds = 1 })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("queue.delay");
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            Assert.Equal(3, application.RowsOf("queue.items").Count);

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the delay to run down and the dance behind it to take over");
        });
    }

    /// <summary>The DJ stops the music for an announcement of unknown length.</summary>
    /// <remarks>
    /// World: a library of two tracks and auto queue off.
    /// Steps: queue a dance, a stop, and another dance, then start the evening and let the first
    /// dance finish.
    /// Sees: the evening waiting on the stop rather than running on into the next dance, and the
    /// next dance starting only when the DJ says so.
    /// </remarks>
    [Fact]
    public async Task DjQueuesAStopAndStartsAgainByHand()
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
            application.Click("queue.stop");
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the stop to come up and the evening to wait on it");

            // Nothing moves on its own from here: the room has the floor for as long as it takes.
            await Task.Delay(500);
            application.Settle();

            Assert.False(
                application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "The dance behind the stop started without anybody asking for it.");

            // A stop is an item like any other, so moving off it is a skip, and a skip with
            // something queued behind it is confirmed.
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to move on from the stop");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the evening to pick up again when the DJ says so");
        });
    }

    /// <summary>The DJ is stopped from playing the same track twice in one evening.</summary>
    /// <remarks>
    /// World: a library of one track, auto queue off, and duplicates refused, which is the default
    /// and the reason a DJ can queue quickly without keeping a list in their head.
    /// Steps: queue the dance, then try to queue it again.
    /// Sees: one entry in the queue, and a message saying why the second one was refused.
    /// </remarks>
    [Fact]
    public async Task DjIsRefusedARepeatOfATrackAlreadyPlayed()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                AllowDuplicateTracksInQueue = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say why the second one was refused");

            Assert.Single(application.RowsOf("queue.items"));
        });
    }

    /// <summary>The DJ fills the queue to the length they set and is stopped there.</summary>
    /// <remarks>
    /// World: a library of three tracks, auto queue off, and a queue of at most two, which is how a
    /// DJ keeps the evening open to what the room asks for next.
    /// Steps: queue three dances.
    /// Sees: two in the queue, and a message about the third rather than a queue that quietly grew.
    /// </remarks>
    [Fact]
    public async Task QueueRefusesTheItemPastTheMaximum()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WithTrack(dance: "Chapelloise", artist: "Duo Absynthe", title: "Le Tourbillon")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false, MaxQueueItems = 2 })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 3,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.DoubleClick(application.Row("catalog.tracks", "Le Tourbillon"));

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say why the third one was refused");

            Assert.Equal(2, application.RowsOf("queue.items").Count);
        });
    }

    /// <summary>The DJ moves a dance up the queue because the room is ready for it now.</summary>
    /// <remarks>
    /// World: a library of two tracks and auto queue off.
    /// Steps: queue both in one order, pick the second, and move it up.
    /// Sees: the queue in the other order.
    /// </remarks>
    [Fact]
    public async Task DjReordersTheQueueBeforeItGetsThere()
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

            Assert.Contains("Salamandre", application.RowsOf("queue.items")[0], StringComparison.Ordinal);

            application.Click(application.Row("queue.items", "La Belle"));
            application.Click("queue.move-up");

            await application.WaitUntil(
                () => application.RowsOf("queue.items")[0].Contains("La Belle", StringComparison.Ordinal),
                "the dance that was moved up to be first");
        });
    }

    /// <summary>The DJ lets the evening carry itself while they talk to somebody.</summary>
    /// <remarks>
    /// World: a library of two tracks, with the auto queue on, which is what a DJ leaves on so the
    /// music never stops dead while they are not looking at the screen.
    /// Steps: queue one dance, start it, and let it finish with nothing behind it.
    /// Sees: the application choosing something itself, and the room still dancing.
    /// </remarks>
    [Fact]
    public async Task TheQueueRefillsItselfWhenItRunsDry()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = true })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the dance the DJ chose to start");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the application to carry on with something of its own");
        });
    }

    /// <summary>The DJ puts a word to the room on the screen for a moment.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off.
    /// Steps: queue a dance, write a message with a couple of seconds on it, and start the evening.
    /// Sees: the message on the playback panel, and the dance behind it taking over when the
    /// message has had its time.
    /// </remarks>
    [Fact]
    public async Task DjPutsAMessageOnTheScreen()
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

            application.Click("queue.message");

            await application.WaitUntil(
                () => application.IsShowing("message.text"),
                "the message to be asked for");

            application.TypeInto("message.text", "Bar closes at eleven");
            application.Click("message.timed");
            application.TypeInto("message.seconds", "2");
            application.Click("message.ok");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.dance").Contains("Bar closes at eleven", StringComparison.Ordinal),
                "the message to go up on the screen");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the message to have its time and the dance behind it to start");
        });
    }

    /// <summary>The DJ leaves a message up until they are ready to take it down.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off.
    /// Steps: write a message with no time on it, queue a dance behind it, and start the evening.
    /// Sees: the message staying up on its own, and the dance starting only when the DJ moves on.
    /// </remarks>
    [Fact]
    public async Task DjLeavesAMessageUpUntilTheyTakeItDown()
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

            application.Click("queue.message");

            await application.WaitUntil(
                () => application.IsShowing("message.text"),
                "the message to be asked for");

            application.TypeInto("message.text", "Lost a green scarf");
            application.Click("message.ok");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.dance").Contains("Lost a green scarf", StringComparison.Ordinal),
                "the message to go up on the screen");

            await Task.Delay(600);
            application.Settle();

            Assert.Contains("Lost a green scarf", application.TextOf("playback.dance"), StringComparison.Ordinal);

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to move on");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the dance to start when the DJ takes the message down");
        });
    }

    /// <summary>The DJ starts the dance again from the top for a room that missed the start.</summary>
    /// <remarks>
    /// World: a library of one dance and auto queue off.
    /// Steps: start the dance, let it run, then restart it and agree when asked.
    /// Sees: the same dance playing from the beginning rather than where it had got to.
    /// </remarks>
    [Fact]
    public async Task DjRestartsTheTrackFromTheTop()
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
                () => application.ProgressOf("playback.progress") > 0.4,
                "the dance to be under way");

            var playedTo = application.ProgressOf("playback.progress");
            application.Click("playback.restart");

            await application.WaitUntil(
                () => application.IsShowing("dialog.confirm"),
                "the application to ask whether to start it again");

            application.Click("dialog.confirm");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") < playedTo,
                "the dance to be back at the beginning");

            Assert.Contains("Salamandre", application.TextOf("playback.track"), StringComparison.Ordinal);
        });
    }

    /// <summary>The DJ is refused a dance that would run past the end of the evening.</summary>
    /// <remarks>
    /// World: a library of one dance, and a cutoff that has just arrived, which is the state a
    /// DJ's evening is in when the hall wants everybody out on the hour.
    /// Steps: try to queue a dance.
    /// Sees: it refused, with the reason on screen, and a queue that is still empty.
    /// </remarks>
    [Fact]
    public async Task DjIsRefusedADanceThatWouldRunPastTheCutoff()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereTheCutoffHasArrived()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say why the dance was refused");

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }

    /// <summary>The grace past the cutoff runs out, and the next dance is refused.</summary>
    /// <remarks>
    /// World: a library of two dances, a cutoff that has just arrived, and the two minutes of grace
    /// a DJ leaves themselves for the dance that is already on the floor.
    /// Steps: queue a dance inside the grace, then let the grace run out and try another.
    /// Sees: the first one allowed and the second refused, which is what the grace is for.
    /// </remarks>
    [Fact]
    public async Task DjIsRefusedADanceThatWouldRunPastTheCutoffGrace()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WhereTheCutoffHasArrived(graceMinutes: 2)
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            Assert.Single(application.RowsOf("queue.items"));

            // The grace is spent while the DJ is looking at the floor.
            RunningApplication.TimePassed(TimeSpan.FromMinutes(3));

            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say why the second dance was refused");

            Assert.Single(application.RowsOf("queue.items"));
        });
    }

    /// <summary>A delay of the length a DJ actually announces, without anybody waiting for it.</summary>
    /// <remarks>
    /// World: a library of two dances and a delay of five minutes, which is what "go and get a
    /// drink" is worth.
    /// Steps: queue a dance, a delay and another dance, start the evening, and let the five minutes
    /// pass.
    /// Sees: the delay giving way to the dance behind it when its time is up rather than when a
    /// second and a half of test audio happens to end.
    /// </remarks>
    [Fact]
    public async Task DjQueuesADelayLongEnoughToBeReal()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false, DelaySeconds = 300 })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.Click("queue.delay");
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the delay to be the thing the room is waiting on");

            RunningApplication.TimePassed(TimeSpan.FromMinutes(6));

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the dance behind the delay to take over once the delay is up");
        });
    }
}
