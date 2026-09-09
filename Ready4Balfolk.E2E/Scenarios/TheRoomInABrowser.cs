using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Playwright;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.E2E.Scenarios;

/// <summary>The pages the hall reads: a laptop at the projector, and a phone in a pocket.</summary>
public sealed class TheRoomInABrowser(HeadlessSession session)
{
    /// <summary>The laptop at the projector follows the evening in a browser.</summary>
    /// <remarks>
    /// World: a library of two dances, the server switched on, and auto queue off.
    /// Steps: open the display page in a browser, then queue a dance on the desktop and start it.
    /// Sees: the page naming the dance being danced and the one behind it, without anybody
    /// touching the browser.
    /// </remarks>
    [Fact]
    public async Task LaptopAtTheProjectorFollowsTheEveningInABrowser()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await using var projector = await TheBrowser.OpenAt(world.ServerAddress);

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.Click("playback.skip");

            await projector.WaitUntilItReads("title", "Salamandre");
            await projector.WaitUntilItReads("nextTitle", "La Belle");
        });
    }

    /// <summary>The helper at the bar unlocks the remote with the PIN the DJ gave them.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote switched on with a PIN.
    /// Steps: open the remote page on a phone and type the PIN in.
    /// Sees: the remote itself, rather than the form asking for the PIN.
    /// </remarks>
    [Fact]
    public async Task HelperUnlocksTheRemoteWithThePin()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "246813")
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "246813");
            await phone.Tap("gateButton");

            await phone.Page.Locator("#app").WaitForAsync();

            Assert.False(await phone.IsShowing("gate"), "The remote still wanted a PIN.");
        });
    }

    /// <summary>A phone that was never given the remote does not find one.</summary>
    /// <remarks>
    /// World: a library of one dance and the server on for the display, with the remote left off,
    /// which is the state a DJ leaves it in when nobody is helping.
    /// Steps: ask for the remote page anyway.
    /// Sees: nothing served, because a remote nobody switched on is not a page that exists.
    /// </remarks>
    [Fact]
    public async Task RemotePageIsNotThereWhenTheRemoteIsOff()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt(world.ServerAddress);

            var asked = await phone.Page.GotoAsync($"{world.ServerAddress}/remote");

            Assert.Equal(404, asked!.Status);
        });
    }

    /// <summary>The helper skips a track from their phone, and the hall hears it.</summary>
    /// <remarks>
    /// World: a library of two dances, the server and the remote on, and auto queue off.
    /// Steps: unlock the remote on a phone, start the evening at the desktop, then hold the skip
    /// button on the phone.
    /// Sees: the desktop moving on to the next dance, which is the whole point of a remote.
    /// </remarks>
    [Fact]
    public async Task HelperSkipsTheTrackFromTheirPhone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "135790")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "135790");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start");

            await phone.HoldDown("skip", TimeSpan.FromMilliseconds(900));

            await application.WaitUntil(
                () => application.TextOf("playback.track").Contains("La Belle", StringComparison.Ordinal),
                "the desktop to move on to what the helper skipped to");
        });
    }

    /// <summary>A phone guessing the PIN is turned away, and then stopped from guessing.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote on with a PIN.
    /// Steps: type the wrong PIN five times.
    /// Sees: each try refused, and the fifth one closing the door for a while rather than letting
    /// the guessing go on: a six digit PIN is only worth anything if nobody may keep trying.
    /// </remarks>
    [Fact]
    public async Task WrongPinIsRefusedAndTheFifthTryLocksOut()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "864209")
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            for (var guess = 0; guess < 4; guess++)
            {
                await phone.TypeInto("pin", "000000");
                await phone.Tap("gateButton");
                await phone.WaitUntilItReads("gateError", "PIN");
            }

            await phone.TypeInto("pin", "000000");
            await phone.Tap("gateButton");

            await phone.WaitUntilItReads("gateError", "Too many tries");

            Assert.True(await phone.IsShowing("gate"), "The remote let a guesser in.");
        });
    }

    /// <summary>The helper asks for something at random from their phone.</summary>
    /// <remarks>
    /// World: a library of two dances, the server and the remote on, and auto queue off so nothing
    /// reaches the queue that nobody asked for.
    /// Steps: unlock the remote and tap the random button on the phone.
    /// Sees: a dance in the desktop's queue that the DJ did not put there.
    /// </remarks>
    [Fact]
    public async Task HelperQueuesARandomTrackFromTheRemote()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "112358")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "112358");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            Assert.Empty(application.RowsOf("queue.items"));

            // The remote has tabs, and asking for something is on the one that adds: a helper taps
            // there first, and so does this.
            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.Page.Locator("[data-act='random']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the dance the helper asked for to reach the queue");
        });
    }

    /// <summary>The helper reads what the next announcement says, on their phone.</summary>
    /// <remarks>
    /// World: a library of one dance, the server and the remote on, and auto queue off.
    /// Steps: unlock the remote, write an announcement on the phone, then have the DJ put a second
    /// one behind it with twenty seconds on it and start the evening.
    /// Sees: the phone's next card billing what is coming as a message and saying what it says,
    /// first with no time on it and then with the time the DJ set. Somebody at the bar decides on
    /// the words whether to fetch the DJ, and a card naming the kind and nothing else sends them
    /// into the queue list to find out what the room is about to be told.
    /// </remarks>
    [Fact]
    public async Task HelperReadsTheNextAnnouncementOnTheirPhone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "998877")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "998877");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            // Writing one is on the tab that adds; reading what is coming is on the first one.
            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.TypeInto("messageText", "Bar closes at eleven");
            await phone.Page.Locator("[data-act='message']").ClickAsync();
            await phone.Page.Locator("[data-tab='now']").ClickAsync();

            await phone.WaitUntilItReads("upnextText", "Bar closes at eleven");

            Assert.Contains(
                UiStrings.Presentation_Message,
                await phone.Reads("upnextText"),
                StringComparison.Ordinal);

            // A second announcement, this one with a time on it, behind the first.
            application.Click("queue.message");

            await application.WaitUntil(
                () => application.IsShowing("message.text"),
                "the message to be asked for");

            application.TypeInto("message.text", "Last dance in five minutes");
            application.Click("message.timed");
            application.TypeInto("message.seconds", "20");
            application.Click("message.ok");

            // Starting the evening puts the first announcement on the screen, which leaves the
            // timed one as what the phone says is coming.
            application.Click("playback.skip");

            await phone.WaitUntilItReads("upnextText", "Last dance in five minutes");

            var expectedTimedLabel = string.Format(CultureInfo.CurrentCulture,
                UiStrings.Presentation_MessageWithDuration,
                string.Format(CultureInfo.CurrentCulture, UiStrings.Presentation_Seconds, 20));

            Assert.Contains(
                expectedTimedLabel,
                await phone.Reads("upnextText"),
                StringComparison.Ordinal);
        });
    }

    /// <summary>The DJ shows a helper where the remote is, without reading out an address.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on and the remote on with a PIN.
    /// Steps: open the toolbar's remote status, then press Escape.
    /// Sees: the address the phone would have to be typed with, and the PIN beside it, and the
    /// window gone again on Escape. Reading "http://192.168.1.42:8420/remote" across a hall and
    /// having somebody type it in the dark is how a helper ends up on the wrong port with the wrong
    /// digit.
    /// </remarks>
    [Fact]
    public async Task DjShowsAHelperTheRemoteAddress()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "202020")
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.IsShowing("toolbar.remote-served"),
                "the toolbar to say the remote is being served");

            Assert.True(application.IsShowing("toolbar.display-served"), "The display page is served too, and said nothing.");

            application.Click("toolbar.remote-served");

            await application.WaitUntil(
                () => application.IsShowing("qr.address"),
                "the address to come up as something a phone can be pointed at");

            Assert.EndsWith("/remote", application.TextOf("qr.address"), StringComparison.Ordinal);
            Assert.Contains(world.WebServerPort.ToString(CultureInfo.InvariantCulture), application.TextOf("qr.address"), StringComparison.Ordinal);
            Assert.Equal("202020", application.TextOf("qr.pin"));

            // The code itself is a picture of that address and nothing else: a name is the only
            // thing anybody not looking at it has to go on.
            Assert.Contains(
                application.TextOf("qr.address"),
                application.NameOf("qr.image"),
                StringComparison.Ordinal);

            // Escape puts it away. It is a window with nothing to answer, standing over the queue
            // in the middle of an evening, and reaching for the mouse to be rid of it is the last
            // thing a DJ with a helper at their elbow has a hand free for.
            application.Press(PhysicalKey.Escape);

            await application.WaitUntil(
                () => !application.IsShowing("qr.address"),
                "the code to go away on Escape");
        });
    }

    /// <summary>The toolbar says nothing about a server that is not running.</summary>
    /// <remarks>
    /// World: a library of one dance and no server at all, which is the default.
    /// Steps: open the application.
    /// Sees: neither status on the toolbar. A toolbar that said "remote" over a server that never
    /// bound would send a hall to an address nothing answers.
    /// </remarks>
    [Fact]
    public async Task TheToolbarSaysNothingWhileTheServerIsOff()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            Assert.False(application.IsShowing("toolbar.display-served"), "A display page nobody is serving was announced.");
            Assert.False(application.IsShowing("toolbar.remote-served"), "A remote nobody is serving was announced.");
        });
    }

    /// <summary>The DJ queues a dance and the screen in the hall keeps up.</summary>
    /// <remarks>
    /// World: a library of two dances, the server on, and auto queue off.
    /// Steps: open the display page, then queue a second dance behind the one that is playing.
    /// Sees: the hall's screen naming what is coming, from a click on the desktop.
    /// </remarks>
    [Fact]
    public async Task DjQueuesADanceAndTheRoomScreenUpdates()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            await using var projector = await TheBrowser.OpenAt(world.ServerAddress);

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await projector.WaitUntilItReads("title", "Salamandre");

            application.DoubleClick(application.Row("catalog.tracks", "La Belle"));

            await projector.WaitUntilItReads("nextTitle", "La Belle");
        });
    }

    /// <summary>The screen in the hall says so when it loses the application.</summary>
    /// <remarks>
    /// World: a library of one dance and the server on, with a browser open at the display.
    /// Steps: switch the server off in the settings, the way a DJ does when they are packing up.
    /// Sees: the page saying it has lost the application rather than standing there showing a dance
    /// that stopped some time ago.
    /// </remarks>
    [Fact]
    public async Task TheDisplaySaysSoWhenItLosesTheApp()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn()
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var projector = await TheBrowser.OpenAt(world.ServerAddress);

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await projector.WaitUntilItReads("title", "Salamandre");

            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.server"),
                "the settings to come up");

            application.Click("settings.server");

            await application.WaitUntil(
                () => application.TextOf("settings.server-status")
                    .Equals(UiStrings.Settings_WebServerStopped, StringComparison.Ordinal),
                "the server to stop");

            await projector.Page.Locator("#lost").WaitForAsync();
        });
    }

    /// <summary>The DJ picks a port somebody else is already using.</summary>
    /// <remarks>
    /// World: a library of one dance, the server switched on, and its port already held by
    /// something else on this machine.
    /// Steps: open the settings and read what the server says it is doing.
    /// Sees: the failure said out loud and the switch back down, rather than a setting that claims
    /// the display page is being served over a server that never started. Ticking it again is the
    /// way to try once more.
    /// </remarks>
    [Fact]
    public async Task DjPicksAPortThatIsAlreadyTaken()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .Save();

        using var squatter = world.WhereSomethingElseHasThePort();
        world.Save();

        await session.RunAsync(world, async application =>
        {
            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.server-status"),
                "the settings to come up");

            await application.WaitUntil(
                () => application.TextOf("settings.server-status").Length > 0
                      && !application.TextOf("settings.server-status")
                          .Equals(UiStrings.Settings_WebServerStarting, StringComparison.Ordinal),
                "the server to say what happened");

            Assert.DoesNotContain(
                UiStrings.Settings_WebServerRunning,
                application.TextOf("settings.server-status"),
                StringComparison.Ordinal);

            // The switch follows what actually happened, and the toolbar says nothing about a page
            // nobody is serving.
            await application.WaitUntil(
                () => application.Find("settings.server") is CheckBox { IsChecked: false },
                "the switch to go back down after the server could not start");

            Assert.False(application.IsShowing("toolbar.display-served"), "A display page nobody is serving was announced.");
        });
    }

    /// <summary>Changing the PIN turns out the phone that had the old one.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote on with a PIN a helper knows.
    /// Steps: unlock the remote on the phone, have the DJ generate a new PIN in the settings, and
    /// touch the phone no further. Then read the new PIN off the desktop and enter it.
    /// Sees: the phone put back to the PIN form on its own and told why, and the new PIN letting
    /// it straight back in. The DJ changes the PIN to take the remote back, so it cannot wait for
    /// the helper to press something; and a helper at the bar can tell being shut out from an
    /// application that has crashed.
    /// </remarks>
    [Fact]
    public async Task ChangingThePinTurnsTheHelperOutOfTheRemote()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "314159")
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "314159");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            application.Click("toolbar.settings");

            await application.WaitUntil(
                () => application.IsShowing("settings.new-pin"),
                "the settings to come up");

            var wasPin = application.TextOf("settings.pin");
            application.Click("settings.new-pin");

            await application.WaitUntil(
                () => !application.TextOf("settings.pin").Equals(wasPin, StringComparison.Ordinal),
                "the DJ to be given a new PIN");

            // Read while the settings are up, and then back to the evening: a queue nobody is
            // looking at is a queue a scenario cannot read either.
            var newPin = application.TextOf("settings.pin");
            application.Click("screen.back");

            await application.WaitUntil(
                () => application.IsShowing("queue.items"),
                "the main screen to come back");

            // Nobody has touched the phone since the PIN changed. It was watching the evening on
            // a socket of its own, and that socket is what a new PIN has to reach.
            await phone.Page.Locator("#gate").WaitForAsync();

            Assert.False(await phone.IsShowing("app"), "The phone still looked like a working remote.");
            Assert.NotEqual(string.Empty, await phone.Reads("gateError"));

            // The gate is the honest answer rather than a dead end: the remote is there, and the
            // helper only needs the PIN that is on the DJ's screen.
            await phone.TypeInto("pin", newPin);
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.Page.Locator("[data-act='random']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the helper to be back in with the new PIN");
        });
    }

    /// <summary>A token from another night puts the PIN form back, rather than a dead remote.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote on with a PIN.
    /// Steps: unlock the remote, let half a day go by, and reload the page the way a phone does when
    /// it wakes up. Then enter the PIN, which has not changed.
    /// Sees: the phone asked for the PIN and told why, and let back in on the PIN that is still
    /// pinned to the DJ's screen. A helper arriving tonight is not looking at a broken application.
    /// </remarks>
    [Fact]
    public async Task AnOldRemoteTokenAsksForThePinAgain()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "161803")
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "161803");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            // The ball is over, the phone slept, and it is the next evening.
            RunningApplication.TimePassed(TimeSpan.FromHours(13));

            await phone.Page.ReloadAsync();
            await phone.Page.Locator("#gate").WaitForAsync();

            Assert.False(await phone.IsShowing("app"), "The phone still looked like a working remote.");
            Assert.NotEqual(string.Empty, await phone.Reads("gateError"));

            await phone.TypeInto("pin", "161803");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.Page.Locator("[data-act='random']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "tonight's PIN to let the same phone back in");
        });
    }

    /// <summary>A helper runs the transport from their phone, the way they run it from the desk.</summary>
    /// <remarks>
    /// World: a library of one dance, the server and the remote on, and auto queue off.
    /// Steps: start the evening at the desktop, then from the phone pause it, resume it, and
    /// restart it from the top.
    /// Sees: the desktop's own progress bar hold while paused, pick back up on resume, and drop
    /// back to the beginning on restart, none of it touched from the desktop itself. The hub methods
    /// behind these three buttons are unit tested; what is not is that the page's play, pause and
    /// restart buttons call them.
    /// </remarks>
    [Fact]
    public async Task HelperRunsTheTransportFromThePhone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "271828")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "271828");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));
            application.Click("playback.skip");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > 0,
                "the dance to get under way");

            // Pause: the desk's own progress bar has to hold, not just the phone's clock.
            await phone.Tap("pp");
            var heldAt = application.ProgressOf("playback.progress");

            await Task.Delay(300);
            application.Settle();

            Assert.Equal(heldAt, application.ProgressOf("playback.progress"));

            // Play again: the desk picks up from where it was held, not from the top.
            await phone.Tap("pp");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > heldAt,
                "the desktop to carry on from where the phone held it");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") > 0.4,
                "the dance to run on far enough for a restart to be visible");
            var playedTo = application.ProgressOf("playback.progress");

            await phone.Tap("restart");

            await application.WaitUntil(
                () => application.ProgressOf("playback.progress") < playedTo,
                "the phone's restart to send the desktop back to the top");

            Assert.Contains("Salamandre", application.TextOf("playback.track"), StringComparison.Ordinal);
        });
    }

    /// <summary>A helper queues a stop, a delay, a message and the closing track from their phone.</summary>
    /// <remarks>
    /// World: a library of one dance, the server and the remote on, a file nominated to close the
    /// night, and auto queue off so nothing reaches the queue that the phone did not put there.
    /// Steps: unlock the remote, move to the tab that adds, and tap each of the four in turn.
    /// Sees: the desktop's own queue grow by one each time, showing the marker for what was tapped,
    /// and the exact words of the message rather than any placeholder.
    /// </remarks>
    [Fact]
    public async Task HelperQueuesAStopADelayAMessageAndTheClosingTrackFromThePhone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithEndOfNightAudio()
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "577215")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 1,
                "the library to be indexed");

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "577215");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            Assert.Empty(application.RowsOf("queue.items"));

            await phone.Page.Locator("[data-tab='add']").ClickAsync();

            await phone.Page.Locator("[data-act='stop']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 1,
                "the stop the phone tapped to reach the queue");
            Assert.Contains(UiStrings.Queue_StopMarker, application.RowsOf("queue.items")[0], StringComparison.Ordinal);

            // The stepper starts at 30 seconds, and the marker's own duration is where that argument
            // shows up on the desktop, so this is what says the phone sent the number it did.
            await phone.Page.Locator("[data-act='delay']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 2,
                "the delay the phone tapped to reach the queue");
            var delayRow = application.RowsOf("queue.items")[1];
            Assert.Contains(UiStrings.Queue_DelayMarker, delayRow, StringComparison.Ordinal);
            Assert.Contains("0:30", delayRow, StringComparison.Ordinal);

            await phone.TypeInto("messageText", "Bar closes at eleven");
            await phone.Page.Locator("[data-act='message']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 3,
                "the message the phone wrote to reach the queue");
            var messageRow = application.RowsOf("queue.items")[2];
            Assert.Contains(UiStrings.Queue_MessageMarker, messageRow, StringComparison.Ordinal);
            Assert.Contains("Bar closes at eleven", messageRow, StringComparison.Ordinal);

            await phone.Page.Locator("[data-act='endofnight']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 4,
                "the closing track the phone tapped to reach the queue");
            Assert.Contains(
                UiStrings.Queue_EndOfNightMarker,
                application.RowsOf("queue.items")[3],
                StringComparison.Ordinal);
        });
    }

    /// <summary>A helper finds a dance from their phone, then moves it up and back down the queue.</summary>
    /// <remarks>
    /// World: a library of two dances, the server and the remote on, and auto queue off. The DJ has
    /// already queued the first one, so the phone is adding to a queue rather than starting one.
    /// Steps: unlock the remote, search for the second dance and tap the hit it turns up, then open
    /// that row in the queue tab and move it up and back down.
    /// Sees: the desktop's own queue gaining exactly the dance that was searched for, then swapping
    /// order on the move up, and swapping back on the move down.
    /// </remarks>
    [Fact]
    public async Task HelperSearchesAndReordersTheQueueFromThePhone()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WithTrack(dance: "Schottische", artist: "Trio Loubelya", title: "La Belle")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "161803")
            .WithSettings(settings => settings with { AutoQueueRandomTrack = false })
            .Save();

        await session.RunAsync(world, async application =>
        {
            await application.WaitUntil(
                () => application.RowsOf("catalog.tracks").Count == 2,
                "the library to be indexed");

            application.DoubleClick(application.Row("catalog.tracks", "Salamandre"));

            await using var phone = await TheBrowser.OpenAt($"{world.ServerAddress}/remote");

            await phone.TypeInto("pin", "161803");
            await phone.Tap("gateButton");
            await phone.Page.Locator("#app").WaitForAsync();

            var hits = phone.Page.Locator("#hits .hit");

            // Opening the tab runs a search of its own with whatever the box holds, which at this
            // point is nothing: that listing has to settle before typing narrows it, or the two
            // requests race and whichever answer lands second is what the phone ends up showing.
            await phone.Page.Locator("[data-tab='find']").ClickAsync();
            await hits.Nth(1).WaitForAsync();

            await phone.TypeInto("search", "Belle");

            // The dance that does not match has to actually leave the list, not merely be joined by
            // the one that does: "La Belle" is in the unfiltered listing too, so waiting for it alone
            // would pass whether or not the term ever reached the search.
            await hits.Filter(new LocatorFilterOptions { HasTextString = "Salamandre" })
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            Assert.Equal(1, await hits.CountAsync());

            await hits.ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items").Count == 2,
                "the dance the phone searched for to reach the queue");
            Assert.Contains("La Belle", application.RowsOf("queue.items")[1], StringComparison.Ordinal);

            await phone.Page.Locator("[data-tab='queue']").ClickAsync();

            var row = phone.Page.Locator("#queueList .qrow")
                .Filter(new LocatorFilterOptions { HasTextString = "La Belle" });
            await row.Locator(".qmain").ClickAsync();
            await row.Locator("[data-move='up']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items")[0].Contains("La Belle", StringComparison.Ordinal),
                "the dance the phone moved up to be first");

            await row.Locator("[data-move='down']").ClickAsync();

            await application.WaitUntil(
                () => application.RowsOf("queue.items")[1].Contains("La Belle", StringComparison.Ordinal),
                "the dance the phone moved back down to be second again");
        });
    }
}
