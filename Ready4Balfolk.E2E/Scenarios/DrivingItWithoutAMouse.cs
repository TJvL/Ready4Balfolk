using System.Globalization;
using Avalonia.Automation;
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

    /// <summary>The queue is rearranged and trimmed from the keyboard.</summary>
    /// <remarks>
    /// World: a library of three dances, auto queue off, all three queued.
    /// Steps: select the last entry, press Ctrl and the up arrow twice, then Delete.
    /// Sees: the entry at the top, and then gone. The keys press the commands the toolbar's buttons
    /// run, and the keyboard stays on the entry as it moves: it used to go with the row the move
    /// replaced, so the second key landed nowhere.
    /// </remarks>
    [Fact]
    public async Task TheQueueIsReorderedAndTrimmedFromTheKeyboard()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Idiosyncrasie")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 3,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.DoubleClick(application.Row("catalog.tracks", "Idiosyncrasie"));

            Assert.Equal(3, application.RowsOf("queue.items").Count);

            application.Click(application.Row("queue.items", "Idiosyncrasie"));
            application.Press(PhysicalKey.ArrowUp, RawInputModifiers.Control);
            await application.WaitUntil(
                () => application.RowsOf("queue.items")[1].Contains("Idiosyncrasie", StringComparison.Ordinal),
                "the entry to move one place up");

            application.Press(PhysicalKey.ArrowUp, RawInputModifiers.Control);
            await application.WaitUntil(
                () => application.RowsOf("queue.items")[0].Contains("Idiosyncrasie", StringComparison.Ordinal),
                "the entry to move to the top on the second press");

            application.Press(PhysicalKey.Delete);

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 2,
                "the entry to be taken out");
            Assert.DoesNotContain(
                application.RowsOf("queue.items"),
                row => row.Contains("Idiosyncrasie", StringComparison.Ordinal));
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
    /// Sees: both taking the keyboard, the dice named after the dance it plays, and a space on it
    /// queueing that dance. The panel draws one dice per dance, so a name that did not say which
    /// one would be the same three words on a hundred buttons.
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

            // The dice is only on the card once the card knows it has something to pick from, and
            // the pool arrives after the dance itself does. It has to stay ready as well as become
            // ready: the panel is fed from four things, two of them throttled, so the first pass
            // to put a dice on screen is not always the last.
            await application.WaitUntilItStays(
                () => application.CanTakeTheKeyboard("dancelist.pick")
                    && application.NameOf("dancelist.pick").Contains("Mazurka", StringComparison.Ordinal),
                "the dice on the card to be there, named after its own dance, and ready for the keyboard");

            application.GiveTheKeyboardTo("dancelist.pick");
            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the dance the keyboard asked for to be in the queue");
        });
    }

    /// <summary>The keyboard stays on a dance card while the panel is brought up to date.</summary>
    /// <remarks>
    /// World: a library of one dance, and a newer dance list on a memory stick.
    /// Steps: stand on that card's dice and move each of the four things the panel watches under
    /// it: what was typed, the library, the pool and the list itself. Then press space.
    /// Sees: the same dice still there after each of them, the keyboard still on it, and the dance
    /// it asks for in the queue. The panel used to build every card again whenever any of the four
    /// moved, so the button somebody had just tabbed to was destroyed under them and the space they
    /// pressed a moment later ran the transport instead of the dice.
    /// The two steps that move the keyboard themselves, because typing and pressing a button are
    /// what they are, ask the other half of the same question: whether the button a DJ was standing
    /// on is still the button that is there afterwards.
    /// </remarks>
    [Fact]
    public async Task TheDanceListKeepsTheKeyboardWhileTheFourThingsItWatchesMove()
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

        var newerList = world.ThePublishedDanceListWithout("mazurka-waltz");

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("catalog.show-dances");

            await application.WaitUntilItStays(
                () => application.CanTakeTheKeyboard("dancelist.pick"),
                "the dice on the one card with a recording behind it to be ready for the keyboard");

            var dice = application.Find("dancelist.pick");

            // What was typed. The caret belongs in the box while somebody is typing into it, so
            // what this asks is that the card underneath was not thrown away and drawn again.
            application.TypeInto("dancelist.search", "Mazurka");

            await application.WaitUntil(
                () => !application.SeesAnywhere("An dro"),
                "the panel to narrow to what was typed");

            Assert.Same(dice, application.Find("dancelist.pick"));

            // The library. A second recording of the dance the keyboard is standing on, put in the
            // music directory the way a DJ puts one there.
            application.GiveTheKeyboardTo(dice);
            world.WithTrack(dance: "Mazurka", artist: "Trio Loubelya", title: "La Belle");

            await application.WaitUntil(
                () => application.SeesAnywhere(
                    string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_TrackCount, 2)),
                "the card to count the recording that has just arrived");

            Assert.True(
                application.TheKeyboardIsOn(dice),
                $"The library arriving took the keyboard off the dice, on to {application.WhateverHasTheKeyboardIsCalled()}.");

            // The pool. A tag on the card itself, which is deliberately not a tab stop: pressing
            // one is something a DJ does with the keyboard somewhere else entirely.
            application.Click("dancelist.card-tag");

            await application.WaitUntil(
                () => !application.SeesAnywhere(UiStrings.DanceList_PoolEverything),
                "the pool to take the tag that was pressed");

            Assert.True(
                application.TheKeyboardIsOn(dice),
                $"The pool changing took the keyboard off the dice, on to {application.WhateverHasTheKeyboardIsCalled()}.");

            // The list itself, imported from a file, which is the fourth of them.
            RunningApplication.TheDjWillPick(newerList);
            application.Click("dancelist.import");

            await application.WaitUntil(
                () => !application.SeesAnywhere("Mazurka-Waltz"),
                "the newer list to arrive and take a dance out of the panel");

            Assert.Same(dice, application.Find("dancelist.pick"));

            // And the whole point of standing on it: a space presses the dice.
            application.GiveTheKeyboardTo(dice);
            application.Press(PhysicalKey.Space);

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the dance the keyboard asked for to be in the queue");
        });
    }

    /// <summary>The keyboard stays with the dance when the panel has to move its card.</summary>
    /// <remarks>
    /// World: a library of one Scottish, and a newer dance list that leads that dance with another
    /// of the spellings it already had, which sorts its card out of the S's and into the E's.
    /// Steps: stand on that card's dice, let the newer list land under it, and press space.
    /// Sees: the dice drawn again as a different control, because Avalonia builds a fresh one for
    /// an item shown at a new index however carefully the card behind it was kept, and the
    /// keyboard on that new one rather than nowhere.
    /// This is the one shape of list change that reorders. A dance added or taken away leaves
    /// every survivor where it was, so keeping the card is enough there and is not enough here.
    /// The list lands without a button being pressed for it, because pressing Import would put the
    /// keyboard on the Import button: what this is about is a DJ whose hands are back in the panel
    /// while the list they asked for a second ago is still on its way.
    /// </remarks>
    [Fact]
    public async Task TheDanceListKeepsTheKeyboardWhileACardMovesUnderIt()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Scottish", artist: "Naragonia", title: "Sinner Man")
            .WhereTheTagsAreTrusted()
            .WithSettings(settings => settings with
            {
                AutoQueueRandomTrack = false,
                RequirePlaybackConfirmation = false
            })
            .Save();

        var respelled = world.ThePublishedDanceListLeadingWith("scottish", "Escoticha");

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            application.Click("catalog.show-dances");

            await application.WaitUntilItStays(
                () => application.CanTakeTheKeyboard("dancelist.pick"),
                "the dice on the one card with a recording behind it to be ready for the keyboard");

            var dice = application.Find("dancelist.pick");
            application.GiveTheKeyboardTo(dice);

            await application.ANewerDanceListArrivesFrom(respelled);

            await application.WaitUntilItStays(
                () => application.SeesAnywhere("Escoticha"),
                "the newer list to arrive and respell the dance the keyboard is standing on");

            // The card was kept and its control was not, which is the whole reason this scenario
            // exists. If this ever fails because the framework started moving containers instead
            // of rebuilding them, the panel is doing work it no longer has to.
            Assert.NotSame(dice, application.Find("dancelist.pick"));

            var moved = application.Find("dancelist.pick");
            Assert.True(
                application.TheKeyboardIsOn(moved),
                $"The card moving took the keyboard off the dice, on to {application.WhateverHasTheKeyboardIsCalled()}.");
            Assert.Contains("Scottish", RunningApplication.NameOf(moved), StringComparison.Ordinal);

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

    /// <summary>The review button says how many tracks are waiting, and review says what it did.</summary>
    /// <remarks>
    /// World: two tracks carrying a dance the published list does not know, so both are waiting
    /// for a person.
    /// Steps: read the review button's name, open review, answer the first row with Enter, step
    /// back up to it and press Enter again.
    /// Sees: the count in the button's name, the summary and the answered row's status as live
    /// regions, and the refused second Enter saying why, out loud. The count is a badge beside the
    /// label and the refusal is a flash, so without these the one says nothing and the other is a
    /// keystroke that reads as never having arrived.
    /// </remarks>
    [Fact]
    public async Task TheReviewButtonCountsWhatIsWaitingAndReviewSaysWhatItDid()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Scottiche", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Scottiche", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .Save();

        var waiting = string.Format(
            CultureInfo.CurrentCulture,
            UiStrings.Toolbar_ReviewNameWaiting,
            string.Format(CultureInfo.CurrentCulture, UiStrings.Toolbar_ReviewCount, 2));

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.NameOf("toolbar.review") == waiting,
                "the review button to say how many tracks are waiting");

            application.Click("toolbar.review");

            await application.WaitUntil(
                () => application.RowsOf("review.rows").Count == 2,
                "both tracks to be waiting for a person");

            Assert.Equal(AutomationLiveSetting.Polite, application.LiveSettingOf("review.summary"));

            // The top row, which is the one the queue opens on and so the one Enter answers.
            var first = application.Rows("review.rows")[0];
            application.GiveTheKeyboardTo(RunningApplication.Within(first, "review.dance"));
            application.Press(PhysicalKey.Enter);

            await application.WaitUntil(
                () => RunningApplication.Says(RunningApplication.Within(first, "review.status"))
                    == UiStrings.Review_ParkedOnUnknownDance,
                "the answered row to say where it now stands");

            Assert.Equal(
                AutomationLiveSetting.Polite,
                RunningApplication.LiveSettingOf(RunningApplication.Within(first, "review.status")));

            // Back to the row that has just been answered, and Enter on it again: an answered row
            // is shut, so this one is refused.
            application.Press(PhysicalKey.ArrowUp);
            application.Press(PhysicalKey.Enter);

            var refusal = RunningApplication.Within(first, "review.refusal");
            Assert.Equal(AutomationLiveSetting.Assertive, RunningApplication.LiveSettingOf(refusal));

            await application.WaitUntil(
                () => RunningApplication.NameOf(refusal) == UiStrings.Review_RefusedAlreadyAnswered,
                "the refusal to say why");
        });
    }

    /// <summary>The first-run wizard says which step it is on, and why it will not go on.</summary>
    /// <remarks>
    /// World: a library of one dance and nothing set up yet.
    /// Steps: walk the wizard with Continue to the step that asks how the library is arranged.
    /// Sees: the step's place, its title and the reason it is blocked all as live regions. The
    /// keyboard stays on Continue the whole way, so nothing it is on says that anything changed.
    /// </remarks>
    [Fact]
    public async Task TheWizardSaysWhichStepItIsOnAndWhyItWillNotGoOn()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereNothingHasBeenSetUpYet()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(() => application.IsShowing("wizard"), "the wizard to open");

            Assert.Equal(AutomationLiveSetting.Polite, application.LiveSettingOf("wizard.progress"));
            Assert.Equal(AutomationLiveSetting.Polite, application.LiveSettingOf("wizard.title"));

            application.Click("wizard.continue");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.browse"),
                "the step that asks where the music is");

            RunningApplication.TheDjWillPick(world.MusicDirectory.FullName);
            application.Click("wizard.browse");
            application.Click("wizard.continue");

            await application.WaitUntil(
                () => application.IsShowing("wizard.blocked"),
                "the step that asks how the library is arranged to say why it will not go on");

            Assert.Equal(AutomationLiveSetting.Polite, application.LiveSettingOf("wizard.blocked"));
        });
    }
}
