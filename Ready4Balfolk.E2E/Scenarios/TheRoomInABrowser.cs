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
                () => application.TextOf("playback.title").Contains("Salamandre", StringComparison.Ordinal),
                "the first dance to start");

            await phone.HoldDown("skip", TimeSpan.FromMilliseconds(900));

            await application.WaitUntil(
                () => application.TextOf("playback.title").Contains("La Belle", StringComparison.Ordinal),
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
    /// Sees: the failure said out loud, rather than a switch that is on above a server that is not.
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
        });
    }

    /// <summary>Changing the PIN turns out the phone that had the old one.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote on with a PIN a helper knows.
    /// Steps: unlock the remote on the phone, then have the DJ generate a new PIN in the settings.
    /// Sees: the phone no longer able to touch the queue, which is what makes changing the PIN a
    /// way of taking the remote back rather than a note for next time. What the phone is told about
    /// it is asserted in #113, where that behaviour changes.
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

            // The phone still has its page, so it can still ask for things. Nothing it asks for
            // may reach the evening.
            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.Page.Locator("[data-act='random']").ClickAsync();

            await Task.Delay(1500);
            application.Settle();

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }

    /// <summary>A phone that was let in last night is asked for the PIN again tonight.</summary>
    /// <remarks>
    /// World: a library of one dance, the server on, and the remote on with a PIN.
    /// Steps: unlock the remote, then let half a day go by and reload the page the way a phone does
    /// when it wakes up.
    /// Sees: nothing the phone asks for reaching the evening, because a token from some other night
    /// opening tonight's queue is exactly what the PIN is there to prevent. What the page says about
    /// it is asserted in #113, where the remote learns to say it has been turned out.
    /// </remarks>
    [Fact]
    public async Task AnOldRemoteTokenIsNoLongerGoodEnough()
    {
        using var world = ScenarioWorld.Create()
            .WithTrack(dance: "Mazurka", artist: "Naragonia", title: "Salamandre")
            .WhereTheTagsAreTrusted()
            .WithTheServerOn(remotePin: "271828")
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

            // The ball is over, the phone slept, and it is the next evening.
            RunningApplication.TimePassed(TimeSpan.FromHours(13));

            await phone.Page.ReloadAsync();
            await phone.Page.Locator("#app").WaitForAsync();

            await phone.Page.Locator("[data-tab='add']").ClickAsync();
            await phone.Page.Locator("[data-act='random']").ClickAsync();

            await Task.Delay(1500);
            application.Settle();

            Assert.Empty(application.RowsOf("queue.items"));
        });
    }
}
