using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Discovery;
using Ready4Balfolk.UI.Views.Help;
using Ready4Balfolk.UI.Views.History;
using Ready4Balfolk.UI.Views.Review;
using Ready4Balfolk.UI.Views.Settings;
using Ready4Balfolk.UI.Views.Wizard;

namespace Ready4Balfolk.Tests.ViewModels;

/// <summary>
/// What the window builds, and when.
/// </summary>
/// <remarks>
/// Everything the main screen shows is constructed with the window; everything behind a button is
/// not. That is the whole behaviour of this class, and it is the kind that goes wrong silently: a
/// screen built eagerly costs a slower start and nothing visible, and a screen rebuilt when it
/// should be kept loses whatever the user had typed on it.
/// </remarks>
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly NavigationService _navigation = new();
    private readonly ILibraryIndex _libraryIndex = Substitute.For<ILibraryIndex>();
    private readonly IDanceListStore _danceListStore = Substitute.For<IDanceListStore>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ITrackStore _trackStore = Substitute.For<ITrackStore>();
    private readonly IPreviewPlaybackService _preview = Substitute.For<IPreviewPlaybackService>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly BehaviorSubject<DanceList> _danceList = new(DanceList.Empty);
    private readonly BehaviorSubject<DanceListStatus> _danceListStatus = new(DanceListStatus.Unknown);
    private readonly List<SetupWizardViewModel> _wizardsBuilt = [];
    private readonly MainWindowViewModel _sut;

    private int _historyBuilt;
    private int _danceListBuilt;
    private int _settingsBuilt;
    private int _helpBuilt;
    private int _reviewBuilt;

    public MainWindowViewModelTests()
    {
        _danceListStore.Current.Returns(_ => _danceList.Value);
        _danceListStore.Observe().Returns(_danceList);
        _danceListStore.ObserveStatus().Returns(_danceListStatus);
        _danceListStore.Status.Returns(_ => _danceListStatus.Value);
        _danceListStore.Index.Returns(_ => DanceListIndex.Build(_danceList.Value));
        _danceListStore.IsLoading.Returns(Observable.Return(false));
        _settingsStore.Current.Returns(new ApplicationSettings());
        _settingsStore.Observe().Returns(Observable.Never<ApplicationSettings>());
        _trackStore.IsLoading.Returns(Observable.Return(false));
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LibraryEntry>());
        _libraryIndex.GetIgnoredValuesAsync(Arg.Any<CancellationToken>()).Returns(new HashSet<string>());
        _preview.WhenPreviewChanged.Returns(Observable.Never<string?>());
        _preview.WhenProgressChanged.Returns(Observable.Never<TimeSpan>());
        _preview.WhenDurationChanged.Returns(Observable.Never<TimeSpan>());

        // The five always-visible panels are handed straight to properties and never touched, so
        // the deferral this class exists for is testable without building any of them.
        _sut = new MainWindowViewModel(
            _navigation,
            toolbar: null!,
            playback: null!,
            equalizer: null!,
            queue: null!,
            trackCatalog: null!,
            Counting<HistoryViewModel>(() => _historyBuilt++),
            Counting<DanceListViewModel>(() => _danceListBuilt++),
            Counting<SettingsViewModel>(() => _settingsBuilt++),
            Counting<HelpViewModel>(() => _helpBuilt++),
            new Lazy<ReviewViewModel>(() =>
            {
                _reviewBuilt++;
                return BuildReview();
            }),
            () =>
            {
                var wizard = BuildWizard();
                _wizardsBuilt.Add(wizard);
                return wizard;
            });
    }

    // --- Nothing before it is asked for ---

    [Fact]
    public void OpeningTheWindow_BuildsNothingBehindAButton()
    {
        // Every one of these reads a store or the disk when it is built, and a first run has none
        // of that yet.
        Assert.Equal(0, _historyBuilt + _danceListBuilt + _settingsBuilt + _helpBuilt + _reviewBuilt);
        Assert.Empty(_wizardsBuilt);
        Assert.Null(_sut.Settings);
        Assert.Null(_sut.Review);
    }

    // --- Screens kept once they are built ---

    [Fact]
    public void Settings_IsBuiltOnceAndKept()
    {
        Visit(Screen.Settings);
        var first = _sut.Settings;
        Visit(Screen.Main);
        Visit(Screen.Settings);

        Assert.Equal(1, _settingsBuilt);
        Assert.Same(first, _sut.Settings);
    }

    [Fact]
    public void Help_IsBuiltOnceAndKept()
    {
        Visit(Screen.Help);
        Visit(Screen.Main);
        Visit(Screen.Help);

        Assert.Equal(1, _helpBuilt);
    }

    [Fact]
    public void Review_IsBuiltOnceButAskedForTheLibraryAgainOnEveryVisit()
    {
        // Kept, because the screen holds a queue of answers in progress; refreshed, because the
        // queue is derived from the index rather than remembered, which is what makes it resumable.
        Visit(Screen.Review);
        var afterFirst = _libraryIndex.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILibraryIndex.SnapshotByPathAsync));
        var first = _sut.Review;

        Visit(Screen.Main);
        Visit(Screen.Review);

        Assert.Equal(1, _reviewBuilt);
        Assert.Same(first, _sut.Review);
        Assert.True(_libraryIndex.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILibraryIndex.SnapshotByPathAsync)) > afterFirst);
    }

    // --- The one screen that is not kept ---

    [Fact]
    public void Setup_IsBuiltFreshEveryTime()
    {
        // Running setup again has to start from what is on disk rather than from where the last
        // visit was abandoned.
        Visit(Screen.Setup);
        var first = _sut.Setup;
        Visit(Screen.Main);
        Visit(Screen.Setup);

        Assert.Equal(2, _wizardsBuilt.Count);
        Assert.NotSame(first, _sut.Setup);
    }

    // --- The two panels on the main screen ---

    [Fact]
    public void History_IsBuiltOnTheFirstToggleAndNotAgain()
    {
        _navigation.IsHistoryMode = true;
        _navigation.IsHistoryMode = false;
        _navigation.IsHistoryMode = true;

        Assert.Equal(1, _historyBuilt);
    }

    [Fact]
    public void DanceList_IsBuiltOnTheFirstToggleAndNotAgain()
    {
        _navigation.IsDanceListMode = true;
        _navigation.IsDanceListMode = false;
        _navigation.IsDanceListMode = true;

        Assert.Equal(1, _danceListBuilt);
    }

    // --- Plumbing ---

    private void Visit(Screen screen) => _navigation.CurrentScreen = screen;

    /// <summary>
    /// A screen this test never looks at, counted rather than built: the window only assigns it.
    /// </summary>
    private static Lazy<T> Counting<T>(Action onBuilt)
        where T : class =>
        new(() =>
        {
            onBuilt();
            return null!;
        });

    private ReviewViewModel BuildReview() => new(
        _libraryIndex,
        _danceListStore,
        _settingsStore,
        _trackStore,
        _preview,
        Substitute.For<INotificationService>(),
        Substitute.For<IConfirmationService>(),
        new DiscoveryViewModel(_settingsStore, _libraryIndex, _danceListStore, _trackStore, new NoOpLoggerService()),
        new NavigationService(),
        new NoOpLoggerService());

    private SetupWizardViewModel BuildWizard() => new(
        new WelcomeStepViewModel(),
        new DanceListStepViewModel(_danceListStore, Substitute.For<IDanceListFeed>(), new FakeTimeProvider()),
        new MusicDirectoryStepViewModel(_settingsStore, _fileSystem),
        new DiscoveryStepViewModel(
            new DiscoveryViewModel(_settingsStore, _libraryIndex, _danceListStore, _trackStore, new NoOpLoggerService())),
        new ReviewStepViewModel(BuildReview()),
        _settingsStore,
        _navigation,
        new NoOpLoggerService());

    public void Dispose()
    {
        _navigation.Dispose();
        _danceList.Dispose();
        _danceListStatus.Dispose();
    }
}
