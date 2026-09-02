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
}
