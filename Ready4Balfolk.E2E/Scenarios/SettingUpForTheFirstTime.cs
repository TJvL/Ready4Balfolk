using Avalonia.Input;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The wizard, walked the way somebody who has never opened this walks it.</summary>
public sealed class SettingUpForTheFirstTime(HeadlessSession session)
{
    /// <summary>A new DJ sets their library up, ticking the one way it is arranged.</summary>
    /// <remarks>
    /// World: a machine nobody has set up, and a music directory of two files named
    /// "Artist - Title".
    /// Steps: through the wizard, pointing it at the music, ticking file name patterns and nothing
    /// else, declaring the shape those files have, and finishing.
    /// Sees: the three ways this library is not arranged staying folded away, the rule accounting
    /// for both files, and the application open on the main screen afterwards.
    /// </remarks>
    [Fact]
    public async Task NewDjSetsUpTheirLibraryForTheFirstTime()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Scottish", artist: "Bal O'Gadjo", title: "Le badaud")
            .WhereNothingHasBeenSetUpYet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(() => application.IsShowing("wizard"), "the wizard to open");

            // Welcome, then the dance list, which this machine already has on disk.
            application.Click("wizard.continue");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.browse"),
                "the step that asks where the music is");

            RunningApplication.TheDjWillPick(world.MusicDirectory.FullName);
            application.Click("wizard.browse");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("discovery.uses-file-names"),
                "the step that asks how the library is arranged");

            // Nothing is ticked, so there is nothing to read this library with and no way on.
            Assert.True(application.IsShowing("wizard.blocked"), "The step let the DJ past with nothing ticked.");
            Assert.False(
                application.IsShowing("discovery.draft-pattern"),
                "A section nobody ticked was asking to be filled in.");

            application.Click("discovery.uses-file-names");

            await application.WaitUntil(
                () => application.IsShowing("discovery.draft-pattern"),
                "the patterns section to open");

            // The other three stay folded away: this library is named, not foldered or tagged.
            Assert.False(application.IsShowing("discovery.custom-tag"), "A section nobody ticked was open.");

            application.TypeInto("discovery.draft-pattern", "%a - %t");
            application.Click("discovery.declare");

            await application.WaitUntil(
                () => application.SeesAnywhere("2 of 2"),
                "the rule to account for both files");

            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "the two files to be waiting with what the rule made of them");

            application.Click("wizard.continue");

            await application.WaitUntil(
                () => !application.IsShowing("wizard") && application.IsShowing("catalog.tracks"),
                "the application to open on the main screen");

            Assert.True(world.SettingsOnDisk().SetupCompleted, "Setup finished without being written down.");
            Assert.True(
                world.SettingsOnDisk().Discovery.UsesFileNamePatterns,
                "The one section the DJ ticked was not saved.");
            Assert.False(
                world.SettingsOnDisk().Discovery.UsesFolderRoles,
                "A section the DJ never ticked was saved as on.");
        });
    }

    /// <summary>Enter in the wizard's own draft-pattern box still means "next step".</summary>
    /// <remarks>
    /// World: a machine nobody has set up, and a music directory of one file.
    /// Steps: through the wizard to the step that asks how the library is arranged, ticking file
    /// name patterns, typing into the draft box and pressing Enter there instead of clicking
    /// Declare or Continue.
    /// Sees: the wizard moving on to the next step the way its own Continue button always did,
    /// with the file still waiting for a person there rather than declared away by a rule the DJ
    /// never asked to add.
    /// </remarks>
    [Fact]
    public async Task EnterInTheWizardsDraftBoxStillMovesToTheNextStep()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereNothingHasBeenSetUpYet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(() => application.IsShowing("wizard"), "the wizard to open");

            application.Click("wizard.continue");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.browse"),
                "the step that asks where the music is");

            RunningApplication.TheDjWillPick(world.MusicDirectory.FullName);
            application.Click("wizard.browse");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("discovery.uses-file-names"),
                "the step that asks how the library is arranged");

            application.Click("discovery.uses-file-names");

            await application.WaitUntil(
                () => application.IsShowing("discovery.draft-pattern"),
                "the patterns section to open");

            application.TypeInto("discovery.draft-pattern", "%a - %t");
            application.Press(PhysicalKey.Enter);

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the wizard's own Continue button to carry the DJ on to the next step");

            Assert.False(
                world.SettingsOnDisk().Discovery.FileNamePatterns.Contains("%a - %t"),
                "Enter in the wizard's draft box declared a rule the DJ never asked to add.");
        });
    }

    /// <summary>Enter on the wizard's last step answers a row instead of finishing setup.</summary>
    /// <remarks>
    /// World: a machine nobody has set up, and a music directory of one tagged track.
    /// Steps: through the wizard to the review queue it ends on, and Enter, twice: once to take the
    /// dance the list offers for what was typed, once to answer the row.
    /// Sees: the caret waiting in the queue rather than nowhere, the track in the library, and the
    /// wizard still open. The queue is answered a row per press for a whole sitting, and the button
    /// that keystroke would otherwise reach on this step is the one that says Finish.
    /// </remarks>
    [Fact]
    public async Task EnterOnTheWizardsReviewStepAnswersTheRowRatherThanFinishing()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereNothingHasBeenSetUpYet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(() => application.IsShowing("wizard"), "the wizard to open");

            application.Click("wizard.continue");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.browse"),
                "the step that asks where the music is");

            RunningApplication.TheDjWillPick(world.MusicDirectory.FullName);
            application.Click("wizard.browse");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("discovery.uses-tags"),
                "the step that asks how the library is arranged");

            application.Click("discovery.uses-tags");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 1,
                "the track to be waiting for a person");

            var row = application.Row("review.rows", "Salamandre");

            // The queue took the keyboard when it appeared. Without this the DJ has to reach for
            // the pointer before the first keystroke does anything, which is the whole cost this
            // screen exists to avoid.
            await application.WaitUntil(
                () => application.TheKeyboardIsOn(RunningApplication.Within(row, "review.dance")),
                $"the caret to start in the row, not on {application.WhateverHasTheKeyboardIsCalled()}");

            application.Type("Mazurka");
            application.Press(PhysicalKey.Enter);
            application.Press(PhysicalKey.Enter);

            await application.WaitUntil(
                () => !RunningApplication.IsShowingWithin(
                    application.Row("review.rows", "Salamandre"), "review.approve"),
                "the row to be answered");

            Assert.True(
                application.IsShowing("wizard"),
                "Enter finished setup instead of answering the row.");
            Assert.False(
                world.SettingsOnDisk().SetupCompleted,
                "Enter finished setup instead of answering the row.");
        });
    }

    /// <summary>The DJ points setup at a folder with nothing in it.</summary>
    /// <remarks>
    /// World: a machine nobody has set up, and a music directory with no music in it.
    /// Steps: the same wizard, pointed at the empty folder, ticking tags because there is nothing
    /// to measure a rule against, and finishing.
    /// Sees: the screen saying there is nothing rather than pretending, and an application that
    /// opens on an empty library instead of on a lie about one.
    /// </remarks>
    [Fact]
    public async Task DjPointsSetupAtAFolderWithNoMusic()
    {
        using var world = ScenarioWorld.Create()
            .WhereNothingHasBeenSetUpYet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(() => application.IsShowing("wizard"), "the wizard to open");

            application.Click("wizard.continue");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.browse"),
                "the step that asks where the music is");

            RunningApplication.TheDjWillPick(world.MusicDirectory.FullName);
            application.Click("wizard.browse");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("discovery.uses-tags"),
                "the step that asks how the library is arranged");

            // No files, so nothing is hidden behind a number: the count is nought of nought.
            Assert.Contains("0", application.TextOf("discovery.coverage"), StringComparison.Ordinal);

            application.Click("discovery.uses-tags");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.SaysAnywhere(UiStrings.Review_Intro),
                "the last step to say there is nothing waiting");

            application.Click("wizard.continue");

            await application.WaitUntil(
                () => !application.IsShowing("wizard") && application.IsShowing("catalog.tracks"),
                "the application to open on the main screen");

            Assert.Empty(application.RowsOf("catalog.tracks"));
        });
    }
}
