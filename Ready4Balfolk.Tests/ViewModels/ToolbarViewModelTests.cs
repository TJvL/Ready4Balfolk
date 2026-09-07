using System.Reactive.Subjects;
using NSubstitute;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Views.Toolbar;
using Ready4Balfolk.Web;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// The review count on the toolbar button.
/// </summary>
/// <remarks>
/// A count on a button is the only way a scan is allowed to mention what it could not place. New
/// files arrive while the application is running in front of a room, and a tagging question during
/// a bal is the worst possible moment to ask one.
/// </remarks>
public sealed class ToolbarViewModelTests : IDisposable
{
    private readonly BehaviorSubject<int> _inReview = new(0);
    private readonly BehaviorSubject<int> _unavailable = new(0);
    private readonly BehaviorSubject<ApplicationSettings> _settings = new(new ApplicationSettings());
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly PresentationWebServer _webServer;
    private readonly ToolbarViewModel _sut;

    public ToolbarViewModelTests()
    {
        _trackStore.InReviewCount.Returns(_inReview);
        _trackStore.UnavailableCount.Returns(_unavailable);

        _settingsStore.Current.Returns(_ => _settings.Value);
        _settingsStore.Observe().Returns(_settings);

        // Never started, so it reports Stopped. Sealed, so there is nothing to substitute, and
        // starting one would mean binding a socket.
        _webServer = new PresentationWebServer(
            Substitute.For<IServiceProvider>(), new NoOpLoggerService(), TimeProvider.System);

        _sut = new ToolbarViewModel(_trackStore, _webServer, _settingsStore);
    }

    [Fact]
    public void NothingWaiting_ShowsNoBadge()
    {
        Assert.Equal(0, _sut.InReviewCount);
        Assert.False(_sut.HasInReview);
        Assert.Equal(string.Empty, _sut.InReviewText);
    }

    [Fact]
    public void SomethingWaiting_ShowsTheCount()
    {
        _inReview.OnNext(7);

        Assert.Equal(7, _sut.InReviewCount);
        Assert.True(_sut.HasInReview);
        Assert.Contains("7", _sut.InReviewText, StringComparison.Ordinal);
    }

    [Fact]
    public void BackToNothing_ClearsTheBadgeRatherThanShowingZero()
    {
        // Answering the last track in review has to leave the toolbar quiet, not showing "0".
        _inReview.OnNext(3);
        _inReview.OnNext(0);

        Assert.False(_sut.HasInReview);
        Assert.Equal(string.Empty, _sut.InReviewText);
    }

    [Fact]
    public void TheSameCountTwice_IsNotRepublished()
    {
        // The library is rebuilt on every approval, and most rebuilds do not change this number.
        var seen = 0;
        using var subscription = _sut.WhenAnyValue(vm => vm.InReviewCount).Subscribe(_ => seen++);

        _inReview.OnNext(4);
        _inReview.OnNext(4);
        _inReview.OnNext(4);

        // The replay of the initial value, then one change.
        Assert.Equal(2, seen);
    }

    [Fact]
    public void NothingUnavailable_SaysNothing()
    {
        Assert.False(_sut.HasUnavailable);
        Assert.Equal(string.Empty, _sut.UnavailableText);
    }

    /// <summary>
    /// A library the application is keeping but cannot reach has to say so somewhere, or a dead
    /// NAS reads as a library that is simply smaller than it was.
    /// </summary>
    [Fact]
    public void TracksTheLibraryCannotReach_AreSaidOutLoud()
    {
        _unavailable.OnNext(19_400);

        Assert.True(_sut.HasUnavailable);
        Assert.Contains("19", _sut.UnavailableText, StringComparison.Ordinal);
    }

    [Fact]
    public void WhenTheyComeBack_TheToolbarGoesQuietAgain()
    {
        _unavailable.OnNext(12);
        _unavailable.OnNext(0);

        Assert.False(_sut.HasUnavailable);
        Assert.Equal(string.Empty, _sut.UnavailableText);
    }

    [Fact]
    public void WithNoServer_TheToolbarSaysNothingAboutWhatIsServed()
    {
        // What is actually being served, not what a switch was set to: this server was never
        // started, so there is nothing for a phone to reach and nothing on the toolbar.
        Assert.False(_sut.IsServingDisplay);
        Assert.False(_sut.IsServingRemote);
    }

    [Fact]
    public void WithNoServer_ThereIsNothingToShowAPhone()
    {
        // Nothing is being served, so there is nothing behind the button and no dialog at all. A
        // running server with no address is the other case, and that one says so on screen.
        Assert.Null(_sut.DisplayAddress());
        Assert.Null(_sut.RemoteAddress());
    }

    /// <summary>
    /// A laptop on nothing at all. A code for localhost scans perfectly and sends the phone to
    /// itself, which looks like it worked, so the dialog opens and says what is wrong instead.
    /// </summary>
    [Fact]
    public async Task ARunningServerWithNoAddress_SaysSoInsteadOfDrawingACode()
    {
        await using var server = await RunningWebServer.StartAsync();
        using var toolbar = new ToolbarViewModel(_trackStore, server.Server, _settingsStore);

        var dialog = toolbar.DisplayAddress();

        Assert.NotNull(dialog);
        Assert.False(dialog.HasCode);
        Assert.Null(dialog.Image);
        Assert.Equal(string.Empty, dialog.Address);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _inReview.Dispose();
        _unavailable.Dispose();
        _settings.Dispose();
        _webServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
