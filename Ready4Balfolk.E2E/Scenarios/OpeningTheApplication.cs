using Avalonia.Controls;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>Starting the application on a machine that is already set up.</summary>
public sealed class OpeningTheApplication(HeadlessSession session)
{
    /// <summary>The DJ opens the application on the library they already have.</summary>
    /// <remarks>
    /// World: a music directory with two tagged tracks, and a settings file in which the DJ has
    /// declared which tag field holds the artist, the title and the dance.
    /// Steps: start the application, and let it find all of that for itself.
    /// Sees: the main screen rather than the setup wizard, with both tracks in the catalogue.
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
            var catalogue = application.Find<DataGrid>("TracksDataGrid");

            await application.WaitUntil(
                () => catalogue.ItemsSource?.Cast<object>().Count() == 2,
                "both tracks to appear in the catalogue");
        });
    }
}
