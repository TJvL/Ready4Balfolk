using Avalonia.Input;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The settings a DJ changes for themselves rather than for one evening.</summary>
public sealed class SettingTheApplicationUpTheirWay(HeadlessSession session)
{
    /// <summary>The DJ switches the application to Dutch and the labels follow.</summary>
    /// <remarks>
    /// World: a library of one dance, on an application in English.
    /// Steps: open the settings and pick Nederlands from the language list.
    /// Sees: the settings page reading in Dutch, without the application needing a restart.
    /// </remarks>
    [Fact]
    public async Task DjSwitchesToDutchAndTheLabelsFollow()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.language"),
                "the settings to come up");

            // Clicked to put the keyboard on it, then chosen with the arrow keys: the list a
            // dropdown opens is a top level of its own, and what is in it belongs to the choice
            // rather than to the window.
            application.Click("settings.language");
            application.Press(PhysicalKey.ArrowDown);
            application.Press(PhysicalKey.Enter);

            await application.WaitUntil(
                () => application.SaysAnywhere(UiStrings.Settings_Language),
                "the settings to read in Dutch");
        });
    }
}
