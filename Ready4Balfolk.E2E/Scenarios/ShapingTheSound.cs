using Avalonia.Controls;
using Ready4Balfolk.Domain.Models.Settings;

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
}
