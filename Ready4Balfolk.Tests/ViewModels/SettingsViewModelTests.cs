using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The settings panel, in the places where it is more than a form over a record.
/// </summary>
/// <remarks>
/// Most of this panel writes a field into the settings and reads it back, which
/// <see cref="Unit.SettingsStoreTests"/> already covers from the other side. What is here is the
/// rest: the debounce that keeps a slider from writing the file on every pixel, the guard that
/// stops a change arriving from the store being written straight back out, the PIN that has to
/// exist before the remote is reachable, a language change that ends in a restart, and asking for a
/// newer dance list.
/// </remarks>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IConfirmationService _confirmations = Substitute.For<IConfirmationService>();
    private readonly IDanceListStore _danceListStore = Substitute.For<IDanceListStore>();
    private readonly BehaviorSubject<DanceListStatus> _danceListStatus = new(DanceListStatus.Unknown);
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ILoggerService _logger = Substitute.For<ILoggerService>();
    private readonly BehaviorSubject<ApplicationSettings> _stored;
    private readonly MockFileSystem _fileSystem = new();
    private readonly ThrottleClock _throttles = new();
    private readonly PresentationWebServer _webServer;
    private readonly SettingsViewModel _sut;

    private ApplicationSettings _settings = new();
    private int _restarts;

    public SettingsViewModelTests()
    {
        _stored = new BehaviorSubject<ApplicationSettings>(_settings);
        _settingsStore.Current.Returns(_ => _settings);
        _settingsStore.Observe().Returns(_stored);
        _settingsStore.UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>())
            .Returns(call =>
            {
                var transform = call.Arg<Func<ApplicationSettings, ApplicationSettings>>()!;
                _settings = transform(_settings);
                return Task.CompletedTask;
            });

        _danceListStore.ObserveStatus().Returns(_danceListStatus);
        _danceListStore.Status.Returns(_ => _danceListStatus.Value);

        // Never started, so it reports Stopped. Sealed, so there is nothing to substitute, and
        // starting one would mean binding a socket.
        _webServer = new PresentationWebServer(
            Substitute.For<IServiceProvider>(), new NoOpLoggerService(), TimeProvider.System);

        _sut = new SettingsViewModel(_settingsStore, _danceListStore, _logger, _notifications, _confirmations,
            _webServer, _fileSystem, () => _restarts++, _throttles.Scheduler);
    }

    /// <summary>Spends the 300ms the panel waits before writing, rather than sleeping past it.</summary>
    private void Settle() => _throttles.LetTheThrottlesRunOut();

    // --- Opening it ---

    [Fact]
    public void Opens_ShowingWhatIsOnDisk()
    {
        Assert.Equal(_settings.MaxQueueItems, _sut.MaxQueueItems);
        Assert.Equal(_settings.ApplicationLanguage, _sut.SelectedLanguage);
        Assert.Equal(_settings.WebServerPort, _sut.WebServerPort);
    }

    [Fact]
    public void Opens_WithoutWritingAnything() =>
        // Reading the panel is not editing it, and the store's own Skip(1) depends on this.
        _settingsStore.DidNotReceive().UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());

    // --- Writing, once ---

    [Fact]
    public async Task AValueDraggedThroughSeveralStops_IsWrittenOnceWhenItSettles()
    {
        // A slider reports every pixel. Without the debounce that is a file write per pixel, and
        // the last one to land wins, which is not necessarily the last one the user chose.
        _sut.MaxQueueItems = 7;
        _sut.MaxQueueItems = 8;
        _sut.MaxQueueItems = 9;
        Settle();

        await _settingsStore.Received(1).UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());
        Assert.Equal(9, _settings.MaxQueueItems);
    }

    [Fact]
    public async Task AChangeThatCameFromTheStore_IsNotWrittenStraightBackOut()
    {
        // The panel listens to the store it writes to. Without the guard, one change from anywhere
        // else in the application becomes a write, which becomes a change, which becomes a write.
        _stored.OnNext(_settings with { MaxQueueItems = 12 });
        Settle();

        Assert.Equal(12, _sut.MaxQueueItems);
        await _settingsStore.DidNotReceive().UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());
    }

    [Fact]
    public async Task ATemplateChangedElsewhere_IsShownHereAndNotWrittenBack()
    {
        // The templates were loaded and saved but never followed, so a change from anywhere else
        // left this screen showing, and on the next keystroke saving, the old one.
        _stored.OnNext(_settings with
        {
            DisplayTemplatesOrNull = _settings.DisplayTemplates with { QueueItem = "%t (%d)" }
        });
        Settle();

        Assert.Equal("%t (%d)", _sut.QueueItemTemplate);
        await _settingsStore.DidNotReceive().UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());
    }

    // --- The remote's PIN ---

    [Fact]
    public void SwitchingTheRemoteOn_MintsAPinInTheSameWrite()
    {
        // There must be no moment where the remote is reachable and the PIN is empty.
        _sut.WebRemoteControlEnabled = true;
        Settle();

        Assert.True(_settings.WebRemoteControlEnabled);
        Assert.NotEmpty(_settings.WebRemoteControlPin);
    }

    [Fact]
    public void SwitchingTheRemoteOn_KeepsAPinYouAlreadyHad()
    {
        // Switching it off and on again must not invalidate the PIN people already typed in.
        _settings = _settings with { WebRemoteControlPin = "123456" };

        _sut.WebRemoteControlEnabled = true;
        Settle();

        Assert.Equal("123456", _settings.WebRemoteControlPin);
    }

    [Fact]
    public void RegeneratePin_ChangesItAndSavesIt()
    {
        _settings = _settings with { WebRemoteControlPin = "123456" };

        _sut.RegeneratePinCommand.Execute().Subscribe();
        Settle();

        Assert.NotEqual("123456", _sut.WebRemoteControlPin);
        Assert.Equal(_sut.WebRemoteControlPin, _settings.WebRemoteControlPin);
    }

    // --- The end of the night audio ---

    [Fact]
    public void AnEndOfNightPathThatIsNotThere_IsSaidSoWhereItWasTyped()
    {
        _sut.EndOfNightAudioPath = "/music/nothing-here.mp3";
        Settle();

        Assert.True(_sut.IsEndOfNightAudioMissing);
    }

    [Fact]
    public void AnEndOfNightPathThatIsThere_IsNotFlagged()
    {
        _fileSystem.AddFile("/music/last-waltz.mp3", new MockFileData([1, 2, 3]));

        _sut.EndOfNightAudioPath = "/music/last-waltz.mp3";
        Settle();

        Assert.False(_sut.IsEndOfNightAudioMissing);
    }

    [Fact]
    public void NoEndOfNightPathAtAll_IsNotAProblem()
    {
        // Empty is the normal state: until somebody says what the sound of the evening ending is,
        // there is nothing to offer to play.
        _sut.EndOfNightAudioPath = "/music/gone.mp3";
        Settle();

        _sut.EndOfNightAudioPath = "";
        Settle();

        Assert.False(_sut.IsEndOfNightAudioMissing);
    }

    // --- The web server ---

    [Fact]
    public void AServerThatWasNeverStarted_SaysStoppedAndOffersNoAddresses()
    {
        Assert.Equal(UiStrings.Settings_WebServerStopped, _sut.WebServerStatus);
        Assert.Equal("", _sut.WebServerAddresses);
        Assert.False(_sut.IsWebServerBusy);
    }

    /// <summary>
    /// The panel prints what another device could type in, best guess first, and never an address
    /// that only works on this machine.
    /// </summary>
    [Fact]
    public async Task ARunningServer_PrintsTheAddressesAPhoneCouldUse()
    {
        await using var server = await RunningWebServer.StartAsync(
            Bridge("172.17.0.1"), Wifi("192.168.1.42"));
        using var panel = Panel(server.Server);

        Assert.Equal(UiStrings.Settings_WebServerRunning, panel.WebServerStatus);
        Assert.Equal(
            $"http://192.168.1.42:{server.Port}{Environment.NewLine}http://172.17.0.1:{server.Port}",
            panel.WebServerAddresses);
    }

    /// <summary>
    /// A laptop on nothing at all. There is no address to hand anybody, so the line says that, and
    /// says where the pages do open: the address block is empty and would otherwise leave the port
    /// to be read off the spinner.
    /// </summary>
    [Fact]
    public async Task ARunningServerWithNoAddress_SaysSoAndStillSaysWhereThePagesOpen()
    {
        await using var server = await RunningWebServer.StartAsync();
        using var panel = Panel(server.Server);

        Assert.Equal(
            string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.Settings_WebServerRunningNoAddress,
                $"http://localhost:{server.Port}"),
            panel.WebServerStatus);
        Assert.Contains($"http://localhost:{server.Port}", panel.WebServerStatus, StringComparison.Ordinal);
        Assert.Equal("", panel.WebServerAddresses);
    }

    private SettingsViewModel Panel(PresentationWebServer webServer) => new(
        _settingsStore, _danceListStore, new NoOpLoggerService(), Substitute.For<INotificationService>(), _confirmations,
        webServer, _fileSystem);

    private static NetworkAdapter Wifi(string address) =>
        new(NetworkInterfaceType.Wireless80211, true, [IPAddress.Parse(address)]);

    /// <summary>A container bridge or a virtual switch: an address, and no router behind it.</summary>
    private static NetworkAdapter Bridge(string address) =>
        new(NetworkInterfaceType.Ethernet, false, [IPAddress.Parse(address)]);

    // --- Changing the language ---

    [Fact]
    public async Task ALanguageChange_AsksFirstBecauseItEndsTheEvening()
    {
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        Settle();

        await _confirmations.Received(1).ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>());
        Assert.Equal(ApplicationLanguage.Dutch, _settings.ApplicationLanguage);
        Assert.Equal(1, _restarts);
    }

    [Fact]
    public async Task ALanguageChange_AsksWithTheKeyboardOnTheSafeAnswer()
    {
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        Settle();

        await _confirmations.Received(1).ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            ConfirmationStakes.Destructive, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ALanguageChange_Declined_PutsTheChoiceBackAndChangesNothing()
    {
        // The dropdown has already moved by the time the question is asked, so saying no has to
        // move it back or the panel is lying about what the application is running.
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        Settle();

        Assert.Equal(ApplicationLanguage.English, _sut.SelectedLanguage);
        Assert.Equal(ApplicationLanguage.English, _settings.ApplicationLanguage);
        Assert.Equal(0, _restarts);
    }

    [Fact]
    public async Task ALanguageChangeBackToWhatItAlreadyIs_AsksNothing()
    {
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        Settle();
        _confirmations.ClearReceivedCalls();

        // Declining put it back to English, which is what the store still says.
        _sut.SelectedLanguage = ApplicationLanguage.English;
        Settle();

        await _confirmations.DidNotReceive().ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmationStakes>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, _restarts);
    }

    // --- The log ---

    [Fact]
    public async Task ExportLog_HandsThePathToTheLogger()
    {
        var logger = Substitute.For<ILoggerService>();
        using var panel = new SettingsViewModel(_settingsStore, _danceListStore, logger, Substitute.For<INotificationService>(),
            _confirmations, _webServer, _fileSystem, () => { });

        await panel.ExportLogAsync("/tmp/ready4balfolk.log");

        await logger.Received(1).ExportAsync("/tmp/ready4balfolk.log");
    }

    // --- Updating the dance list ---

    [Fact]
    public async Task UpdateDanceList_Updated_SaysHowManyAreNew()
    {
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Updated(4));

        await _sut.UpdateDanceListCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Updated, 4),
            NotificationSeverity.Information);
    }

    [Fact]
    public async Task UpdateDanceList_AlreadyCurrent_SaysSoWithoutFuss()
    {
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Unchanged);

        await _sut.UpdateDanceListCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(UiStrings.DanceList_AlreadyCurrent, NotificationSeverity.Information);
    }

    [Fact]
    public async Task UpdateDanceList_Failed_IsAWarningRatherThanAnError()
    {
        // A hall with no wifi is the normal case. The list already in hand carries on working, so
        // this is not the application breaking.
        _danceListStatus.OnNext(new DanceListStatus(3, 3, DanceListOrigin.Cached, DateTimeOffset.UnixEpoch));
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Failed("no route to host"));

        await _sut.UpdateDanceListCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, "no route to host"),
            NotificationSeverity.Warning);
    }

    [Fact]
    public async Task UpdateDanceList_FailedWithNoListInHand_DoesNotClaimOneIsStillInUse()
    {
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Failed("no route to host"));

        await _sut.UpdateDanceListCommand.Execute().FirstAsync();

        _notifications.Received(1).Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_NoneArrived, "no route to host"),
            NotificationSeverity.Warning);
    }

    [Fact]
    public async Task UpdateDanceList_WhileItRuns_TheButtonsSaySo()
    {
        var fetching = new TaskCompletionSource<DanceListUpdate>();
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(fetching.Task);

        var running = _sut.UpdateDanceListCommand.Execute().FirstAsync().ToTask();
        Assert.True(_sut.IsUpdatingDanceList);

        fetching.SetResult(DanceListUpdate.Unchanged);
        await running;

        Assert.False(_sut.IsUpdatingDanceList);
    }

    [Fact]
    public async Task UpdateDanceListFromFile_TheFileIsUnreadable_IsReportedOnceRatherThanThrown()
    {
        // The path came from a file picker, so anything can be behind it, including a directory.
        // One file, one notice, in the words a refusal would have used and in the DJ's language.
        // What .NET said about it goes to the log with an English line, and never to the screen.
        _danceListStatus.OnNext(new DanceListStatus(3, 3, DanceListOrigin.Cached, DateTimeOffset.UnixEpoch));
        var thrown = new IOException("that is a folder");
        _danceListStore.UpdateFromFileAsync(Arg.Any<IFileInfo>(), Arg.Any<CancellationToken>())
            .Returns<Task<DanceListUpdate>>(_ => throw thrown);

        await _sut.UpdateDanceListFromFileAsync("/somewhere/dances.json");

        await _logger.Received(1).ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        await _logger.Received(1).ErrorAsync("Failed to update the dance list from a file", thrown);
        _notifications.Received(1).Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
        _notifications.Received(1).Show(
            string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, DomainStrings.DanceList_FileUnreadable),
            NotificationSeverity.Error);
        Assert.False(_sut.IsUpdatingDanceList);
    }

    // --- Where the dance list came from ---

    [Fact]
    public void DanceListOrigin_NoListYet_SaysSo() =>
        Assert.Equal(UiStrings.DanceList_NoListYet, _sut.DanceListOriginText);

    [Fact]
    public void DanceListOrigin_Downloaded_SaysWhen()
    {
        // A stale list has to be visible rather than assumed: it is the vocabulary everything else
        // in the application is said in.
        var obtained = new DateTimeOffset(2026, 8, 20, 19, 30, 0, TimeSpan.Zero);
        _danceListStatus.OnNext(new DanceListStatus(3, 3, DanceListOrigin.Downloaded, obtained));

        Assert.Equal(
            string.Format(ApplicationCulture.Current, UiStrings.DanceList_Obtained, obtained.ToLocalTime().DateTime),
            _sut.DanceListOriginText);
    }

    [Theory]
    [InlineData("en-US", "nl")]
    [InlineData("nl-NL", "en")]
    public void DanceListOrigin_NamesTheMonthInTheApplicationsLanguage(string machine, string application)
    {
        using var cultures = new CultureScope(machine, application);
        var obtained = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

        var origin = DanceListReports.Origin(new DanceListStatus(3, 3, DanceListOrigin.Downloaded, obtained));

        // "opgehaald 15 October 2026" was the Dutch application on an English laptop.
        Assert.Contains(obtained.ToString("MMMM", CultureInfo.GetCultureInfo(application)), origin, StringComparison.Ordinal);
        Assert.DoesNotContain(obtained.ToString("MMMM", CultureInfo.GetCultureInfo(machine)), origin, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _stored.Dispose();
        _danceListStatus.Dispose();
        _webServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
