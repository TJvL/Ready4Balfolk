namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>What the application makes of the machine it is started on.</summary>
public sealed class StartingOnWhatIsThere(HeadlessSession session)
{
    /// <summary>A DJ who has set up before is not asked to do it again.</summary>
    /// <remarks>
    /// World: a machine that has been through setup, with a music directory and tags it trusts.
    /// Steps: start the application.
    /// Sees: the evening's own screen, and no wizard between them and it.
    /// </remarks>
    [Fact]
    public async Task ReturningDjIsNotAskedToSetUpAgain()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library the DJ already had");

            Assert.False(application.IsShowing("wizard"), "A DJ who is set up was asked to set up.");
        });
    }

    /// <summary>The settings file is unreadable, and the application still starts.</summary>
    /// <remarks>
    /// World: a data directory whose settings file was left half written, which is what an
    /// interrupted write or a full disk leaves behind.
    /// Steps: start the application.
    /// Sees: a window, and the setup a fresh machine gets, rather than a start that dies on the
    /// way up.
    /// </remarks>
    [Fact]
    public async Task SettingsOnDiskAreCorruptAndTheAppStartsAnyway()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereTheSettingsFileIsCorrupt();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("wizard"),
                "the application to come up and offer to set itself up");

            Assert.True(application.Window.IsVisible, "The application did not come up at all.");
        });
    }

    /// <summary>A track is renamed outside the application and the library follows it.</summary>
    /// <remarks>
    /// World: a library of two dances, on tags the DJ has declared.
    /// Steps: rename one of the files in another window, the way tidying up does.
    /// Sees: the same two dances in the catalogue, because what makes a track is what is in it
    /// rather than what it is called.
    /// </remarks>
    [Fact]
    public async Task DjRenamesAFileOutsideTheAppAndTheLibraryFollows()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            world.RenameTrackFile("Salamandre", "01 - the mazurka we open with.mp3");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2
                      && application.RowsOf("catalog.tracks")
                          .Any(row => row.Contains("Salamandre", StringComparison.Ordinal)),
                "the library to follow the file to its new name");
        });
    }

    /// <summary>A machine nobody has given a dance list cannot answer for anything.</summary>
    /// <remarks>
    /// World: a music directory with a track in it, tags the DJ trusts, and no dance list, which is
    /// what a fresh machine is now that the application ships none.
    /// Steps: start the application and look at the library.
    /// Sees: nothing in the catalogue and the track waiting in review, because a dance nobody can
    /// name is not something to guess at.
    /// </remarks>
    [Fact]
    public async Task DjStartsWithNoDanceListAndCannotAnswerAnything()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereThereIsNoDanceList()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("catalog.tracks"),
                "the main screen");

            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting, since there is no vocabulary to place it in");

            application.Click("screen.back");

            Assert.Empty(application.RowsOf("catalog.tracks"));
        });
    }

    /// <summary>The hall has no wifi, and the DJ is told that fetching the list did not work.</summary>
    /// <remarks>
    /// World: a machine with no dance list and no way to reach BigBalfolkList, which is a cellar
    /// with a laptop in it.
    /// Steps: open the dance list panel and ask for the published list.
    /// Sees: a message saying it could not be had, and still no list, rather than a panel that
    /// looks like it worked.
    /// </remarks>
    [Fact]
    public async Task DanceListRefreshFailsAndTheDjIsToldSo()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereThereIsNoDanceList()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("catalog.tracks"),
                "the main screen");

            application.Click("catalog.show-dances");
            application.Click("dancelist.update");

            await application.WaitUntil(
                () => application.IsShowing("notification.message"),
                "the application to say that the list could not be fetched");

            Assert.False(application.SeesAnywhere("Mazurka"), "A dance list arrived from nowhere.");
        });
    }

    /// <summary>The DJ fetches the published list, and the machine has a vocabulary.</summary>
    /// <remarks>
    /// World: a machine with no dance list, and a network, which is the one scenario here that
    /// needs one: this is the act of reaching BigBalfolkList.
    /// Steps: open the dance list panel and ask for the published list.
    /// Sees: the dances arriving, and the panel showing them.
    /// </remarks>
    [Fact]
    public async Task DjFetchesTheDanceListFromBigBalfolkList()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereThereIsNoDanceList()
            .WhereThereIsInternet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("catalog.tracks"),
                "the main screen");

            application.Click("catalog.show-dances");
            application.Click("dancelist.update");

            await application.WaitUntil(
                () => application.SeesAnywhere("Mazurka"),
                "the published list to arrive and be shown");
        });
    }
}
