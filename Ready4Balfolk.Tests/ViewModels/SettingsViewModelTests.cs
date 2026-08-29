using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
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
/// exist before the remote is reachable, and a language change that ends in a restart.
/// </remarks>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IConfirmationService _confirmations = Substitute.For<IConfirmationService>();
    private readonly BehaviorSubject<ApplicationSettings> _stored;
    private readonly MockFileSystem _fileSystem = new();
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

        // Never started, so it reports Stopped. Sealed, so there is nothing to substitute, and
        // starting one would mean binding a socket.
        _webServer = new PresentationWebServer(
            Substitute.For<IServiceProvider>(), new NoOpLoggerService());

        _sut = new SettingsViewModel(_settingsStore, new NoOpLoggerService(), _confirmations,
            _webServer, _fileSystem, () => _restarts++);
    }

    /// <summary>Longer than the 300ms the panel waits before writing.</summary>
    private static async Task SettleAsync() => await Task.Delay(500);

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
        await SettleAsync();

        await _settingsStore.Received(1).UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());
        Assert.Equal(9, _settings.MaxQueueItems);
    }

    [Fact]
    public async Task AChangeThatCameFromTheStore_IsNotWrittenStraightBackOut()
    {
        // The panel listens to the store it writes to. Without the guard, one change from anywhere
        // else in the application becomes a write, which becomes a change, which becomes a write.
        _stored.OnNext(_settings with { MaxQueueItems = 12 });
        await SettleAsync();

        Assert.Equal(12, _sut.MaxQueueItems);
        await _settingsStore.DidNotReceive().UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>());
    }

    // --- The remote's PIN ---

    [Fact]
    public async Task SwitchingTheRemoteOn_MintsAPinInTheSameWrite()
    {
        // There must be no moment where the remote is reachable and the PIN is empty.
        _sut.WebRemoteControlEnabled = true;
        await SettleAsync();

        Assert.True(_settings.WebRemoteControlEnabled);
        Assert.NotEmpty(_settings.WebRemoteControlPin);
    }

    [Fact]
    public async Task SwitchingTheRemoteOn_KeepsAPinYouAlreadyHad()
    {
        // Switching it off and on again must not invalidate the PIN people already typed in.
        _settings = _settings with { WebRemoteControlPin = "123456" };

        _sut.WebRemoteControlEnabled = true;
        await SettleAsync();

        Assert.Equal("123456", _settings.WebRemoteControlPin);
    }

    [Fact]
    public async Task RegeneratePin_ChangesItAndSavesIt()
    {
        _settings = _settings with { WebRemoteControlPin = "123456" };

        _sut.RegeneratePinCommand.Execute().Subscribe();
        await SettleAsync();

        Assert.NotEqual("123456", _sut.WebRemoteControlPin);
        Assert.Equal(_sut.WebRemoteControlPin, _settings.WebRemoteControlPin);
    }

    // --- The end of the night audio ---

    [Fact]
    public async Task AnEndOfNightPathThatIsNotThere_IsSaidSoWhereItWasTyped()
    {
        _sut.EndOfNightAudioPath = "/music/nothing-here.mp3";
        await SettleAsync();

        Assert.True(_sut.IsEndOfNightAudioMissing);
    }

    [Fact]
    public async Task AnEndOfNightPathThatIsThere_IsNotFlagged()
    {
        _fileSystem.AddFile("/music/last-waltz.mp3", new MockFileData([1, 2, 3]));

        _sut.EndOfNightAudioPath = "/music/last-waltz.mp3";
        await SettleAsync();

        Assert.False(_sut.IsEndOfNightAudioMissing);
    }

    [Fact]
    public async Task NoEndOfNightPathAtAll_IsNotAProblem()
    {
        // Empty is the normal state: until somebody says what the sound of the evening ending is,
        // there is nothing to offer to play.
        _sut.EndOfNightAudioPath = "/music/gone.mp3";
        await SettleAsync();

        _sut.EndOfNightAudioPath = "";
        await SettleAsync();

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

    // --- Changing the language ---

    [Fact]
    public async Task ALanguageChange_AsksFirstBecauseItEndsTheEvening()
    {
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        await SettleAsync();

        await _confirmations.Received(1).ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal(ApplicationLanguage.Dutch, _settings.ApplicationLanguage);
        Assert.Equal(1, _restarts);
    }

    [Fact]
    public async Task ALanguageChange_Declined_PutsTheChoiceBackAndChangesNothing()
    {
        // The dropdown has already moved by the time the question is asked, so saying no has to
        // move it back or the panel is lying about what the application is running.
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        await SettleAsync();

        Assert.Equal(ApplicationLanguage.English, _sut.SelectedLanguage);
        Assert.Equal(ApplicationLanguage.English, _settings.ApplicationLanguage);
        Assert.Equal(0, _restarts);
    }

    [Fact]
    public async Task ALanguageChangeBackToWhatItAlreadyIs_AsksNothing()
    {
        _confirmations.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        _sut.SelectedLanguage = ApplicationLanguage.Dutch;
        await SettleAsync();
        _confirmations.ClearReceivedCalls();

        // Declining put it back to English, which is what the store still says.
        _sut.SelectedLanguage = ApplicationLanguage.English;
        await SettleAsync();

        await _confirmations.DidNotReceive().ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.Equal(0, _restarts);
    }

    // --- The log ---

    [Fact]
    public async Task ExportLog_HandsThePathToTheLogger()
    {
        var logger = Substitute.For<ILoggerService>();
        using var panel = new SettingsViewModel(_settingsStore, logger, _confirmations,
            _webServer, _fileSystem, () => { });

        await panel.ExportLogAsync("/tmp/ready4balfolk.log");

        await logger.Received(1).ExportAsync("/tmp/ready4balfolk.log");
    }

    public void Dispose()
    {
        _sut.Dispose();
        _stored.Dispose();
        _webServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
