namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The same evening, with the world underneath it not as it was left.</summary>
public sealed class WhenTheWorldIsBroken(HeadlessSession session)
{
    /// <summary>The DJ asks for something at random from a library that has nothing in it.</summary>
    /// <remarks>
    /// World: a music directory with no music in it, which is what a mistyped path or an unmounted
    /// drive leaves behind.
    /// Steps: ask the queue for a random track.
    /// Sees: a message saying there is nothing to pick from, and a queue that is still empty.
    /// </remarks>
    [Fact]
    public async Task TheLibraryIsEmptyWhenTheQueueAsksForARandomTrack()
    {
        using var world = ScenarioWorld.Create()
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("queue.random");

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say there is nothing to pick from");

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }

    /// <summary>A file in the music directory turns out not to be audio at all.</summary>
    /// <remarks>
    /// World: a music directory holding one real track and one file that is named like audio and is
    /// not, which is what a failed download or a copy that was interrupted leaves behind.
    /// Steps: start the application and let it index what is there.
    /// Sees: the real track in the catalogue, and the library no smaller for the other one being
    /// unreadable.
    /// </remarks>
    [Fact]
    public async Task AFileThatWillNotDecodeIsReportedNotIndexed()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithUnreadableTrack("half a download.mp3")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the one real track to reach the catalogue");

            Assert.Contains(
                application.RowsOf("catalog.tracks"),
                row => row.Contains("Salamandre", StringComparison.Ordinal));
        });
    }

    /// <summary>A track is queued, and its file is gone by the time the room gets to it.</summary>
    /// <remarks>
    /// World: a library of two dances, on tags the DJ trusts.
    /// Steps: queue both, take the first one's file away in another window, and start the evening.
    /// Sees: the queue losing the entry as the file goes, and the evening carrying on into the
    /// dance behind it, rather than stopping on an item that can never start.
    /// </remarks>
    [Fact]
    public async Task ATrackIsQueuedAndItsFileHasVanished()
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

            world.RemoveTrackFile("Salamandre");

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the queue to lose the entry whose file has gone");

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.title").Contains("La Belle", StringComparison.Ordinal),
                "the evening to carry on with the dance that is still there");
        });
    }

    /// <summary>The closing track's file goes while the evening is running.</summary>
    /// <remarks>
    /// World: a library with one dance and a file nominated as the sound of the evening ending,
    /// which lives outside the music directory and so is watched by nothing.
    /// Steps: queue the closing track, take its file away, and reach it.
    /// Sees: the application saying it could not be played, and the evening moving on rather than
    /// stopping on something that can never start.
    /// </remarks>
    [Fact]
    public async Task TheClosingTrackCannotBePlayedAndTheEveningMovesOn()
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

            application.Click("queue.end-of-night");
            world.RemoveTheEndOfNightFile();

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say that the closing track could not be played");

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }
}
