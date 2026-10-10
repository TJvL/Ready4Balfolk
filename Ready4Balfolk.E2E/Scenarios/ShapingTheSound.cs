using System.Globalization;
using Avalonia.Controls;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>Correcting the room from the equalizer panel, rather than proving the effect chain in isolation.</summary>
public sealed class ShapingTheSound(HeadlessSession session)
{
    /// <summary>A preamp trim survives a restart, in both directions of that round trip.</summary>
    /// <remarks>
    /// World: a library of one dance, a preamp already saved from a previous evening, and auto
    /// queue off.
    /// Steps: start the application on that world and open the equalizer panel, which is the read
    /// half of a restart: from the application's own perspective there is no difference between
    /// this and actually having been closed and reopened, since both are a startup reading the
    /// same settings file. Then play the dance, pull the preamp to a new value, and watch it reach
    /// the settings file, which is the write half: whatever a restart would read back is only ever
    /// what a save like this one put there.
    /// Sees: the panel opening with the saved trim already on the sliders, then the new trim
    /// landing on disk once it is pulled.
    ///
    /// A literal close-then-reopen inside one scenario was tried and does not work here: BASS is
    /// process wide, and a second startup's own attempt to initialise it fails with "Bass.Init
    /// failed: Already" because the first one is never freed mid scenario. <see cref="HeadlessSession"/>
    /// exists for exactly this reason: every scenario gets a process of its own so that BASS,
    /// started once, is freed by the process ending rather than by anything in this codebase. A
    /// second application inside that same process is the "several in a row" its own remarks
    /// describe breaking, so the two halves of the round trip are proven with one application
    /// rather than two.
    ///
    /// The trim is also read back off the audio engine rather than only off the file, because a
    /// number saved correctly into a settings file that nothing downstream reads is exactly the
    /// failure a panel can have while every unit test passes. Ready4Balfolk.Domain makes the
    /// playback service's stream handles visible to this project for that read.
    /// </remarks>
    [Fact]
    public async Task DjPullsThePreampDownAndItSurvivesARestart()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -3 }
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("equalizer.expander");

            await application.WaitUntil(
                () => application.IsShowing("equalizer.preamp"),
                "the equalizer panel to open");

            // What a restart would show, read off the same file a restart would read.
            Assert.True(
                application.Find("equalizer.enable") is CheckBox { IsChecked: true },
                "The equalizer opened switched off, though the world it started on had it on.");
            Assert.Equal(-3, application.ValueOf("equalizer.preamp"));

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > 0,
                "the dance to get under way");

            application.Move("equalizer.preamp", -6);

            // What a restart would then read back, once one happens: the file this write leaves
            // behind, at the value the slider was pulled to rather than the one it opened on.
            await application.WaitUntil(
                () => world.SettingsOnDisk().Equalizer is { Enabled: true, PreampDecibels: -6 },
                "the preamp to reach the settings file");

            // And what the room hears, which the file cannot answer for. Half the amplitude, which
            // is what -6 dB is as the trim BASS holds it in.
            var trim = Math.Pow(10, -6 / 20.0);
            await application.WaitUntil(
                () => Math.Abs(RunningApplication.TrimOnThePlayingStream() - trim) < 0.001,
                "the trim the DJ pulled to reach the stream that is playing");
        });
    }

    /// <summary>One recording gets an equalizer of its own, and the panel says so while it plays.</summary>
    /// <remarks>
    /// World: two tracks, and a global equalizer switched on with a preamp of -3 dB.
    /// Steps: give one track its own equalizer in the edit dialog with the preamp at -9, play it,
    /// pull its preamp on the main screen to -12, open the edit dialog on it again, and then press
    /// Back to global.
    /// Sees: while it plays, the panel named after the track with a Back to global link, its own
    /// -9 on the sliders and on the stream; the pull to -12 reaching the stream and the track
    /// rather than the global equalizer; and Back to global putting the panel's plain name, the
    /// global -3 and the global trim back. The preamp is what is read off the stream, as above.
    /// </remarks>
    [Fact]
    public async Task DjGivesOneRecordingItsOwnEqualizer()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Mazurka", artist: "Duo Absynthe", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                EqualizerOrNull = EqualizerSettings.Flat with { Enabled = true, PreampDecibels = -3 }
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await OpenTheEditorOn(application, "Salamandre");
            application.Click("edit-track.own-equalizer");
            application.Move("edit-track.equalizer-preamp", -9);
            application.Click("edit-track.save");
            await application.WaitUntil(
                () => !application.IsShowing("edit-track.own-equalizer"),
                "the dialog to close");

            application.Click("equalizer.expander");
            await application.WaitUntil(
                () => application.IsShowing("equalizer.preamp"),
                "the equalizer panel to open");
            Assert.Equal(UiStrings.Equalizer_Title, application.TextOf("equalizer.header"));

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            var ownHeader = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Equalizer_TrackHeader, "Naragonia", "Salamandre");
            await application.WaitUntil(
                () => application.TextOf("equalizer.header") == ownHeader,
                "the panel to name the track playing through its own equalizer");
            Assert.True(application.IsShowing("equalizer.back-to-global"));
            Assert.Equal(-9, application.ValueOf("equalizer.preamp"));
            await application.WaitUntil(
                () => Math.Abs(RunningApplication.TrimOnThePlayingStream() - Math.Pow(10, -9 / 20.0)) < 0.001,
                "the track's own preamp to reach the stream");

            application.Move("equalizer.preamp", -12);
            await application.WaitUntil(
                () => Math.Abs(RunningApplication.TrimOnThePlayingStream() - Math.Pow(10, -12 / 20.0)) < 0.001,
                "the pull on the track's own preamp to reach the stream");

            // Long enough for the throttled write to land, and the global equalizer it must not
            // land in stays exactly where it was.
            await application.NeverHappensWithin(
                TimeSpan.FromSeconds(1),
                () => world.SettingsOnDisk().Equalizer.PreampDecibels != -3,
                "the global preamp to move with a pull on a track's own");

            await OpenTheEditorOn(application, "Salamandre");
            Assert.Equal(-12, application.ValueOf("edit-track.equalizer-preamp"));
            application.Click("edit-track.cancel");
            await application.WaitUntil(
                () => !application.IsShowing("edit-track.own-equalizer"),
                "the dialog to close");

            application.Click("equalizer.back-to-global");

            await application.WaitUntil(
                () => application.TextOf("equalizer.header") == UiStrings.Equalizer_Title,
                "the panel to go back to the global equalizer");
            Assert.False(application.IsShowing("equalizer.back-to-global"));
            Assert.Equal(-3, application.ValueOf("equalizer.preamp"));
            await application.WaitUntil(
                () => Math.Abs(RunningApplication.TrimOnThePlayingStream() - Math.Pow(10, -3 / 20.0)) < 0.001,
                "the global preamp to be back on the stream");
        });
    }

    /// <summary>A curve made for one recording is carried to another through the clipboard.</summary>
    /// <remarks>
    /// World: two tracks, the first already given its own equalizer.
    /// Steps: copy its settings in the edit dialog, open the other track and paste them, save, and
    /// open the second track again.
    /// Sees: the second track with its own equalizer switched on and the first one's curve on it.
    /// </remarks>
    [Fact]
    public async Task DjCopiesOneRecordingsEqualizerOntoAnother()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Mazurka", artist: "Duo Absynthe", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await OpenTheEditorOn(application, "Salamandre");
            application.Click("edit-track.own-equalizer");
            application.Move("edit-track.equalizer-preamp", -7);
            application.Click("edit-track.copy-equalizer");
            application.Click("edit-track.save");
            await application.WaitUntil(
                () => !application.IsShowing("edit-track.own-equalizer"),
                "the dialog to close");

            await OpenTheEditorOn(application, "La Belle");
            Assert.True(application.Find("edit-track.own-equalizer") is CheckBox { IsChecked: false });
            application.Click("edit-track.paste-equalizer");
            await application.WaitUntil(
                () => application.Find("edit-track.own-equalizer") is CheckBox { IsChecked: true },
                "the pasted settings to switch the track's own equalizer on");
            Assert.Equal(-7, application.ValueOf("edit-track.equalizer-preamp"));
            // Only so there is something on screen to wait for: the catalogue shows the new title
            // once the library has been rebuilt, and that one rebuild carries the curve as well.
            application.TypeInto("edit-track.title", "La Belle Copied");
            application.Click("edit-track.save");

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks")
                    .Any(row => row.Contains("La Belle Copied", StringComparison.Ordinal)),
                "the library to be rebuilt with what was saved");
            await OpenTheEditorOn(application, "La Belle Copied");
            Assert.True(application.Find("edit-track.own-equalizer") is CheckBox { IsChecked: true });
            Assert.Equal(-7, application.ValueOf("edit-track.equalizer-preamp"));
            application.Click("edit-track.cancel");
        });
    }

    private static async Task OpenTheEditorOn(RunningApplication application, string title)
    {
        var row = application.Row("catalog.tracks", title);
        application.Click(row);
        application.RightClick(row);
        application.Click("catalog.edit-track");

        await application.WaitUntil(
            () => application.IsShowing("edit-track.own-equalizer"),
            "the edit dialog to come up");
    }
}
