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

    /// <summary>The hall has no wifi, and the evening runs on the list the application shipped with.</summary>
    /// <remarks>
    /// World: a machine with no dance list of its own and nothing to download one with, which is a
    /// cellar with a laptop in it.
    /// Steps: start the application.
    /// Sees: a dance vocabulary anyway, and a library that fills, because the list the build ships
    /// with is a real one rather than a placeholder.
    /// </remarks>
    [Fact]
    public async Task DanceListRefreshFailsAndTheOldListStays()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WhereThereIsNoInternet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the track to reach the catalogue on the list the build shipped with");

            application.Click("catalog.show-dances");

            await application.WaitUntil(
                () => application.SeesAnywhere("Mazurka") && application.SeesAnywhere("Scottish"),
                "the dance list panel to be showing the dances the build shipped with");
        });
    }
}
