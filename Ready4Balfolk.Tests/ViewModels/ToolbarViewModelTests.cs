using System.Reactive.Subjects;
using NSubstitute;
using ReactiveUI.Reactive;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
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
    private readonly PresentationWebServer _webServer;
    private readonly ToolbarViewModel _sut;

    public ToolbarViewModelTests()
    {
        var trackStore = Substitute.For<ITrackStore>();
        trackStore.InReviewCount.Returns(_inReview);
        trackStore.UnavailableCount.Returns(_unavailable);

        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(_ => _settings.Value);
        settingsStore.Observe().Returns(_settings);

        // Never started, so it reports Stopped. Sealed, so there is nothing to substitute, and
        // starting one would mean binding a socket.
        _webServer = new PresentationWebServer(
            Substitute.For<IServiceProvider>(), new NoOpLoggerService(), TimeProvider.System);

        _sut = new ToolbarViewModel(trackStore, _webServer, settingsStore);
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
    public void WithNoAddress_ThereIsNothingToShowAPhone()
    {
        // A server that never bound has no address, and an empty code is worse than no code.
        Assert.Null(_sut.DisplayAddress());
        Assert.Null(_sut.RemoteAddress());
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
