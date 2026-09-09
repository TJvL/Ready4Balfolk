namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>Starting the application on a machine that is already set up.</summary>
public sealed class OpeningTheApplication(HeadlessSession session)
{
    /// <summary>The DJ opens the application on the library they already have.</summary>
    /// <remarks>
    /// World: a music directory with two tagged tracks, and a settings file in which the DJ has
    /// declared which tag field holds the artist, the title and the dance.
    /// Steps: start the application, and let it find all of that for itself.
    /// Sees: the main screen rather than the setup wizard, with both tracks in the catalogue,
    /// under the dance names the published list spells them with.
    /// </remarks>
    [Fact]
    public async Task DjOpensTheApplicationOnTheirOwnLibrary()
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
                "both tracks to appear in the catalogue");

            var catalogue = application.RowsOf("catalog.tracks");

            Assert.Contains(catalogue, row => row.Contains("Naragonia", StringComparison.Ordinal)
                                              && row.Contains("Salamandre", StringComparison.Ordinal)
                                              && row.Contains("Mazurka", StringComparison.Ordinal));
            Assert.Contains(catalogue, row => row.Contains("Trio Loubelya", StringComparison.Ordinal)
                                              && row.Contains("La Belle", StringComparison.Ordinal));
        });
    }

    /// <summary>The DJ sorts the catalog by a column, and a third click hands it back unsorted.</summary>
    /// <remarks>
    /// World: three tracks whose dance, artist and title never agree on an order, so any two of
    /// the three orderings below being equal would be the columns not doing anything rather than
    /// this test being unable to tell them apart.
    /// Steps: click the Artist column header once, again, and a third time.
    /// Sees: the rows ascending by artist, then descending, then back to the order the catalog is
    /// built in, which is by dance: the third click does not restore the second-to-last state, it
    /// clears the sort entirely.
    /// </remarks>
    [Fact]
    public async Task DjSortsTheCatalogByAColumnAndClearsItAgain()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Chapelloise", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Mazurka", artist: "Trio Loubelya", title: "La Belle")
            .WithTrack(dance: "Waltz", artist: "Duo Absynthe", title: "Lumieres")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 3,
                "all three tracks to appear in the catalogue");

            // Built by dance, ascending: Chapelloise, Mazurka, Waltz.
            var builtOrder = application.RowsOf("catalog.tracks");
            Assert.Equal(["Salamandre", "La Belle", "Lumieres"], TitlesOf(builtOrder));

            application.Click("catalog.column-artist");
            Assert.Equal(
                ["Lumieres", "Salamandre", "La Belle"],
                TitlesOf(application.RowsOf("catalog.tracks")));

            application.Click("catalog.column-artist");
            Assert.Equal(
                ["La Belle", "Salamandre", "Lumieres"],
                TitlesOf(application.RowsOf("catalog.tracks")));

            application.Click("catalog.column-artist");
            Assert.Equal(builtOrder, application.RowsOf("catalog.tracks"));
        });
    }

    /// <summary>The title each row of a catalog listing carries, top to bottom.</summary>
    private static IReadOnlyList<string> TitlesOf(IReadOnlyList<string> rows) =>
        [.. rows.Select(row => row switch
        {
            _ when row.Contains("Salamandre", StringComparison.Ordinal) => "Salamandre",
            _ when row.Contains("La Belle", StringComparison.Ordinal) => "La Belle",
            _ => "Lumieres"
        })];
}
