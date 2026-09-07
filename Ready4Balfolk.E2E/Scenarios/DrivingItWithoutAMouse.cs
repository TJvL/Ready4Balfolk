using Avalonia.Controls;
using Avalonia.Input;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>
/// The evening as it is run standing up, in the dark, with one hand on a mixer, and the evening as
/// it reads to somebody who is not looking at the screen at all.
/// </summary>
public sealed class DrivingItWithoutAMouse(HeadlessSession session)
{
    /// <summary>The DJ queues, starts and skips without ever reaching for the pointer.</summary>
    /// <remarks>
    /// World: a library of two dances, auto queue off, and no confirmations in the way.
    /// Steps: walk into the library with the keyboard, answer two rows with Enter, start the
    /// evening with Space and move on with Ctrl and the right arrow.
    /// Sees: both dances in the queue, the first playing and then the second. Nothing here was
    /// clicked, which is the point: the hand that would hold the mouse is on a mixer.
    /// </remarks>
    [Fact]
    public async Task DjRunsTheEveningFromTheKeyboard()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
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
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.GiveTheKeyboardTo("catalog.tracks");

            // Down twice for two rows, Enter on each: walking a list and taking what is
            // highlighted is the whole of putting an evening together.
            application.Press(PhysicalKey.ArrowDown);
            application.Press(PhysicalKey.Enter);
            application.Press(PhysicalKey.ArrowDown);
            application.Press(PhysicalKey.Enter);

            Assert.Equal(2, application.RowsOf("queue.items").Count);

            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start on a space");

            application.Press(PhysicalKey.ArrowRight, RawInputModifiers.Control);

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the second dance to start");
        });
    }

    /// <summary>The search box is a key away, and the transport keeps out of it.</summary>
    /// <remarks>
    /// World: a library of two dances, auto queue off, with one of them already in the queue so
    /// there is something a stray space could start.
    /// Steps: press Ctrl+F from the library, type a title, then press the space bar.
    /// Sees: the caret in the box, the library narrowed to what was typed, and still nothing
    /// playing. The keys that run the evening are the keys somebody searching types, so a space
    /// that started the music from inside the search box would make the box unusable.
    /// </remarks>
    [Fact]
    public async Task TheSearchBoxIsAKeyAwayAndKeepsWhatIsTypedIntoIt()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
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
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            application.GiveTheKeyboardTo("catalog.tracks");
            application.Press(PhysicalKey.F, RawInputModifiers.Control);
            application.Type("La Belle");

            Assert.Equal("La Belle", application.TextOf("catalog.search"));

            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to narrow to what was typed");

            // The space bar itself, with the caret in the box. A key press and the character it
            // produces reach a window as two separate events, so the half a scenario can ask
            // about is this one: the key did not run the transport out from under the typing.
            application.Press(PhysicalKey.Space);

            Assert.False(application.IsShowing("playback.track"), "A space in the search box started the music.");

            // And from the other panel, which is the case that needs the library brought back
            // first: a search box behind a hidden panel is not somewhere a caret can go.
            application.Click("catalog.show-dances");

            await application.WaitUntil(
                () => !application.IsShowing("catalog.search"),
                "the dance list to take the right column");

            application.Press(PhysicalKey.F, RawInputModifiers.Control);
            application.Type("Salamandre");

            Assert.Equal("Salamandre", application.TextOf("catalog.search"));
        });
    }

    /// <summary>A space presses what is under it rather than starting the music.</summary>
    /// <remarks>
    /// World: a library of one dance, auto queue off.
    /// Steps: put a dance in the queue so there is something to play, then press space with the
    /// keyboard on a button and again with it on the list of nights.
    /// Sees: the button pressed, the list open, and nothing playing either time. The transport is
    /// what a space does when nothing else on screen has a use for one, never a key taken off the
    /// control the DJ is standing on.
    /// </remarks>
    [Fact]
    public async Task ASpaceBelongsToWhateverIsStandingUnderIt()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
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
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            // A button first: a space on one of these is how a button is pressed, everywhere.
            application.GiveTheKeyboardTo("queue.stop");
            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 2,
                "the stop the space asked for to be in the queue");

            Assert.False(application.IsShowing("playback.track"), "The space started the music instead.");

            application.Click("queue.show-history");

            await application.WaitUntil(
                () => application.IsShowing("history.nights"),
                "the nights to come up");

            application.GiveTheKeyboardTo("history.nights");
            application.Press(PhysicalKey.Space);

            Assert.True(
                ((ComboBox)application.Find("history.nights")).IsDropDownOpen,
                "The space never reached the list of nights.");
            Assert.False(application.IsShowing("playback.track"), "The space started the music instead.");
        });
    }

    /// <summary>Every button says what it is, with nothing but an icon drawn on it.</summary>
    /// <remarks>
    /// World: a library of one dance, with button text off, which is how it ships.
    /// Steps: read the names off the toolbar and the transport, then start the evening and read
    /// the transport again.
    /// Sees: a name on every one of them, and the two-state buttons saying which state they are
    /// in. A path drawn in a button carries no text at all, so without these a screen reader
    /// finds a row of buttons it cannot tell apart or name.
    /// </remarks>
    [Fact]
    public async Task EveryButtonSaysWhatItIsWithTheLabelsOff()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                ShowButtonText = false,
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            Assert.Equal(UiStrings.Toolbar_HelpLabel, application.NameOf("toolbar.help"));
            Assert.Equal(UiStrings.Toolbar_SettingsLabel, application.NameOf("toolbar.settings"));
            Assert.Equal(UiStrings.Toolbar_ReviewLabel, application.NameOf("toolbar.review"));
            Assert.Equal(UiStrings.QueueToolbar_StopLabel, application.NameOf("queue.stop"));
            Assert.Equal(UiStrings.QueueToolbar_RandomLabel, application.NameOf("queue.random"));
            Assert.Equal(UiStrings.Playback_RestartLabel, application.NameOf("playback.restart"));

            // The two states of the two buttons that have them. Nothing is queued yet, so the one
            // beside play is the one that clears rather than the one that moves on.
            Assert.Equal(UiStrings.Playback_PlayLabel, application.NameOf("playback.play-pause"));
            Assert.Equal(UiStrings.Playback_ClearLabel, application.NameOf("playback.skip"));

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            await application.WaitUntil(
                () => application.NameOf("playback.skip") == UiStrings.Playback_SkipLabel,
                "the button beside play to say it now moves on");

            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.NameOf("playback.play-pause") == UiStrings.Playback_PauseLabel,
                "the play button to say it now pauses");
        });
    }

    /// <summary>A review row is walked to its last button and answered, without a pointer.</summary>
    /// <remarks>
    /// World: two tracks carrying the same misspelling of a dance, so the row offers everything it
    /// has: answer it, use the name for every track saying the same thing, take a spelling the list
    /// does carry, or say the value is not a dance at all.
    /// Steps: put the keyboard on the queue, walk a row with Tab, and press the button that says
    /// the value is not a dance.
    /// Sees: the three boxes and then every button that answers with them, the keyboard leaving the
    /// row at the end of it, and the value gone from both tracks. Tab that goes round three boxes
    /// for ever is a row a keyboard can get into and never out of, and every button on it is then
    /// a thing only a mouse can press.
    /// </remarks>
    [Fact]
    public async Task AReviewRowIsWalkedToItsLastButtonAndAnswered()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Scottiche", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Scottiche", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "both tracks to be waiting for a person");

            // Tab through the screen stops on a row of the list, and the press after that has to
            // go into the row rather than straight past the whole list: everything a row is
            // answered with is inside it.
            var first = application.Row("review.rows", "Salamandre");
            application.GiveTheKeyboardTo(first);
            application.Press(PhysicalKey.Tab);

            Assert.True(
                application.TheKeyboardIsOn(first),
                $"Tab went past the list, on to {application.WhateverHasTheKeyboardIsCalled()}.");
            Assert.NotEqual(first, application.WhatHasTheKeyboard());

            // And an arrow from there puts the caret in the next row's first box.
            application.Press(PhysicalKey.ArrowDown);

            var row = application.Row("review.rows", "La Belle");
            Assert.True(
                application.TheKeyboardIsOn(RunningApplication.Within(row, "review.dance")),
                $"The arrow left the keyboard on {application.WhateverHasTheKeyboardIsCalled()}.");

            // Every stop Tab makes, until it leaves the row: a walk that never leaves is the trap,
            // and one that leaves before the buttons is what makes them pointer-only.
            var walk = new List<string>();
            for (var press = 0; press < 12 && application.TheKeyboardIsOn(row); press++)
            {
                application.Press(PhysicalKey.Tab);
                if (application.TheKeyboardIsOn(row))
                {
                    walk.Add(application.WhateverHasTheKeyboardIsCalled());
                }
            }

            var walked = string.Join(" -> ", walk);
            Assert.False(application.TheKeyboardIsOn(row), $"Tab never left the row: {walked}");

            // Out of the whole list rather than on into the row below: the queue holds a track per
            // question the library asked, so a Tab that walked them all would be its own trap.
            Assert.False(
                application.TheKeyboardIsOn(application.Find("review.rows")),
                $"Tab walked out of the row and into the queue: {application.WhateverHasTheKeyboardIsCalled()}.");
            Assert.Equal(
                ["review.artist", "review.title", "review.approve", "review.use-for-all"],
                walk.Take(4));
            Assert.Equal("review.not-a-dance", walk[^1]);

            // And what the list thinks the misspelling meant, in between: the offered spellings
            // were the one thing on this row with no key of any kind on it.
            Assert.True(walk.Count > 5, $"No offered spelling was walked through: {walked}");

            // And the button it reached is a button. Enter, which this queue otherwise keeps for
            // answering the selected row: on a button it presses the button, or Tab would arrive
            // somewhere the only key that presses things does something else entirely.
            application.GiveTheKeyboardTo(RunningApplication.Within(row, "review.not-a-dance"));
            application.Press(PhysicalKey.Enter);

            await application.WaitUntil(
                () => application.Rows("review.rows")
                    .All(waiting => RunningApplication.Says(RunningApplication.Within(waiting, "review.dance")).Length == 0),
                "the value to be gone from both tracks");
        });
    }

    /// <summary>The dance list is set from the keyboard, and says which dance each button is for.</summary>
    /// <remarks>
    /// World: a library of one dance, which is enough for one card to carry the dice.
    /// Steps: open the dance list, narrow it to one dance, and put the keyboard on the tag rail and
    /// on that card's dice.
    /// Sees: both taking the keyboard, and the dice named after the dance it plays. The panel draws
    /// one dice per dance, so a name that did not say which one would be the same three words on a
    /// hundred buttons.
    /// </remarks>
    [Fact]
    public async Task TheDanceListTakesTheKeyboardAndSaysWhichDanceEachButtonIsFor()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("catalog.show-dances");

            await application.WaitUntil(
                () => application.IsShowing("dancelist.search"),
                "the dance list to take the right column");

            // The rail, where a tag is put in the pool. One chip per tag rather than the copies
            // the cards carry, which is what makes it somewhere Tab can afford to stop.
            application.GiveTheKeyboardTo("dancelist.tag");

            application.TypeInto("dancelist.search", "Mazurka");

            await application.WaitUntil(
                () => application.NameOf("dancelist.pick").Contains("Mazurka", StringComparison.Ordinal),
                "the dice on the card to be named after its own dance");

            application.GiveTheKeyboardTo("dancelist.pick");
            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the dance the keyboard asked for to be in the queue");
        });
    }

    /// <summary>A setting is ticked with the keyboard, in a panel that had no tab stops at all.</summary>
    /// <remarks>
    /// World: a library of one dance, with the moment between dances switched off.
    /// Steps: open the settings, put the keyboard on the tick box and press space.
    /// Sees: the setting on, and written down. The transport keys are deliberately not in the way
    /// here: this screen is read and typed into rather than played from, so a space belongs to
    /// whatever has the keyboard.
    /// </remarks>
    [Fact]
    public async Task ASettingIsTickedWithoutAMouse()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { GapBetweenTracksEnabled = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.gap-between-tracks"),
                "the settings to come up");

            application.GiveTheKeyboardTo("settings.gap-between-tracks");
            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => world.SettingsOnDisk().GapBetweenTracksEnabled,
                "the tick to be written down");
        });
    }
}
