using Avalonia.Input;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The settings a DJ changes for themselves rather than for one evening.</summary>
public sealed class SettingTheApplicationUpTheirWay(HeadlessSession session)
{
    /// <summary>The DJ writes tracks their own way, and every screen that writes one follows.</summary>
    /// <remarks>
    /// World: a library of three dances, auto queue off, and no confirmations in the way.
    /// Steps: play one dance through so the night has something in it, start a second and queue a
    /// third, then rewrite the templates in the settings.
    /// Sees: the line while it is playing, the row in the queue and the row in the history all
    /// written the new way. The track overview is not one of them: it is a table somebody sorts by
    /// dance, artist or title, and a column is not a sentence.
    /// </remarks>
    [Fact]
    public async Task DjChangesHowTracksAreNamedOnScreen()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WithTrack(dance: "Waltz", artist: "Duo Absynthe", title: "Lumieres")
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
                () => application.RowsOf("catalog.tracks").Count == 3,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => !application.IsShowing("playback.track"),
                "the first dance to play out and reach the night");

            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.DoubleClick(application.Row("catalog.tracks", "Lumieres"));
            application.Click("playback.skip");

            await application.WaitUntil(
                // The name the shared list carries: the tag says Schottische, the vocabulary
                // calls it Scottish.
                () => application.TextOf("playback.dance").Contains("Scottish", StringComparison.Ordinal),
                "the second dance to start");

            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.template-now-playing"),
                "the settings to come up");

            application.TypeInto("settings.template-now-playing", "%t by %a");
            application.TypeInto("settings.template-queue", "%t by %a");
            application.TypeInto("settings.template-history", "%t by %a");

            application.Click("screen.back");

            await application.WaitUntil(
                () => application.TextOf("playback.dance") == "La Belle by Trio Loubelya",
                "the line while it is playing to be written the new way");

            // Waited for rather than asserted outright: each box saves on its own once the typing
            // has stopped, so the screens follow one after another rather than all at once.
            await application.WaitUntil(
                () => application.RowsOf("queue.items").Any(row =>
                    row.Contains("Lumieres by Duo Absynthe", StringComparison.Ordinal)),
                "the queue to be written the new way");

            application.Click("queue.show-history");

            await application.WaitUntil(
                () => application.RowsOf("history.items").Any(row =>
                    row.Contains("Salamandre by Naragonia", StringComparison.Ordinal)),
                "the night to read the new way too");

            // The overview keeps its columns, so the dance is still a field of its own to sort by.
            application.Click("history.show-queue");

            Assert.Contains(
                application.RowsOf("catalog.tracks"),
                row => row.Contains("Mazurka", StringComparison.Ordinal)
                       && row.Contains("Naragonia", StringComparison.Ordinal));
        });
    }

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
