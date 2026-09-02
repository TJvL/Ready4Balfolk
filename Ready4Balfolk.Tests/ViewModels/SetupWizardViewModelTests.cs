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
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Discovery;
using Ready4Balfolk.UI.Views.Review;
using Ready4Balfolk.UI.Views.Wizard;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class SetupWizardViewModelTests : IDisposable
{
    private readonly IDanceListStore _danceListStore = Substitute.For<IDanceListStore>();
    private readonly BehaviorSubject<DanceList> _danceListSubject = new(DanceList.Empty);
    private readonly BehaviorSubject<DanceListStatus> _statusSubject = new(DanceListStatus.Unknown);
    private readonly IDanceListFeed _feed = Substitute.For<IDanceListFeed>();
    private readonly NavigationService _navigation = new();
    private readonly IPreviewPlaybackService _preview = Substitute.For<IPreviewPlaybackService>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly FakeTimeProvider _now = new();
    private readonly SetupWizardViewModel _sut;
    private ApplicationSettings _settings = new();

    public SetupWizardViewModelTests()
    {
        _danceListStore.Current.Returns(_ => _danceListSubject.Value);
        _danceListStore.Observe().Returns(_danceListSubject);
        _danceListStore.ObserveStatus().Returns(_statusSubject);
        _danceListStore.Status.Returns(_ => _statusSubject.Value);
        _danceListStore.Index.Returns(_ => DanceListIndex.Build(_danceListSubject.Value));
        _danceListStore.IsLoading.Returns(Observable.Return(false));
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>()).Returns(DanceListUpdate.Unchanged);
        _feed.HomePage.Returns(new Uri("https://example.invalid/list"));

        _settingsStore.Current.Returns(_ => _settings);
        _settingsStore.UpdateAsync(Arg.Any<Func<ApplicationSettings, ApplicationSettings>>())
            .Returns(callInfo =>
            {
                // Arg<T> is unconstrained, so the compiler reads it as possibly null; the call it
                // matched always supplied a transform.
                var transform = callInfo.Arg<Func<ApplicationSettings, ApplicationSettings>>()!;
                _settings = transform(_settings);
                return Task.CompletedTask;
            });

        _sut = BuildWizard();
    }

    private SetupWizardViewModel BuildWizard()
    {
        var logger = new NoOpLoggerService();
        var notifications = Substitute.For<INotificationService>();

        var libraryIndex = Substitute.For<ILibraryIndex>();
        libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, LibraryEntry>());
        libraryIndex.GetIgnoredValuesAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());

        _preview.WhenPreviewChanged.Returns(Observable.Never<string?>());
        _preview.WhenProgressChanged.Returns(Observable.Never<TimeSpan>());
        _preview.WhenDurationChanged.Returns(Observable.Never<TimeSpan>());

        var trackStore = Substitute.For<ITrackStore>();
        trackStore.IsLoading.Returns(Observable.Return(false));

        var discovery = new DiscoveryViewModel(_settingsStore, libraryIndex, _danceListStore, trackStore, logger);
        var review = new ReviewViewModel(libraryIndex, _danceListStore, _settingsStore, trackStore, _preview, notifications, Substitute.For<IConfirmationService>(), discovery, new NavigationService(), logger);

        return new SetupWizardViewModel(
            new WelcomeStepViewModel(),
            new DanceListStepViewModel(_danceListStore, _feed, _now),
            new MusicDirectoryStepViewModel(_settingsStore, _fileSystem),
            new DiscoveryStepViewModel(discovery),
            new ReviewStepViewModel(review),
            _settingsStore,
            _navigation,
            logger);
    }

    [Fact]
    public void StartsOnAnExplanation()
    {
        Assert.IsType<WelcomeStepViewModel>(_sut.CurrentStep);
        Assert.True(_sut.IsFirstStep);
        Assert.False(_sut.IsLastStep);
    }

    [Fact]
    public void TheFirstStepAsksNothing() => Assert.True(CanContinueNow());

    [Fact]
    public void TheDanceListStepWillNotBePassedWithoutAList()
    {
        GoTo<DanceListStepViewModel>();

        // Nothing ships with the application, so a machine arrives here with no vocabulary at all,
        // and everything after this step needs one.
        Assert.False(CanContinueNow());
        Assert.True(_sut.IsBlocked);
        Assert.NotEqual(string.Empty, _sut.BlockedReason);
    }

    [Fact]
    public void TheDanceListStepIsPassedOnceAListHasArrived()
    {
        _statusSubject.OnNext(new DanceListStatus(113, 40, DanceListOrigin.Downloaded, _now.GetUtcNow()));

        GoTo<DanceListStepViewModel>();

        Assert.True(CanContinueNow());
    }

    [Fact]
    public void TheDanceListStepFetchesNothingOnItsOwn()
    {
        GoTo<DanceListStepViewModel>();

        // Reaching out is an act the user takes, not something a step does on the way past.
        _danceListStore.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheDanceListStepFetchesWhenItIsAskedTo()
    {
        var step = GoTo<DanceListStepViewModel>();

        step.FetchCommand.Execute().Subscribe();

        _danceListStore.Received().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheDanceListStepDoesNotFetchWhatWasJustFetched()
    {
        // Stepping back and forward over this page, or pressing the button twice, is not a reason
        // to ask BigBalfolkList for the same file again.
        _statusSubject.OnNext(new DanceListStatus(113, 40, DanceListOrigin.Downloaded, _now.GetUtcNow()));

        var step = GoTo<DanceListStepViewModel>();

        step.FetchCommand.Execute().Subscribe();

        _danceListStore.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheDanceListStepFetchesAgainOnceThatIsStale()
    {
        _statusSubject.OnNext(new DanceListStatus(
            113, 40, DanceListOrigin.Downloaded, _now.GetUtcNow() - TimeSpan.FromHours(2)));

        var step = GoTo<DanceListStepViewModel>();

        step.FetchCommand.Execute().Subscribe();

        _danceListStore.Received().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheDanceListStepSurvivesAFailedFetch()
    {
        _danceListStore.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(DanceListUpdate.Failed("no network"));

        var step = GoTo<DanceListStepViewModel>();

        step.FetchCommand.Execute().Subscribe();

        // A hall with no wifi is not a dead end: the step is still there to try again, or to take
        // a file instead. It is still blocked, because there is still no list.
        Assert.False(step.IsFetching);
        Assert.False(CanContinueNow());
    }

    [Fact]
    public void TheMusicStepWillNotBePassedWithoutAFolder()
    {
        GoTo<MusicDirectoryStepViewModel>();

        // Everything after this reads the library, so there is nothing to set up without one.
        Assert.False(CanContinueNow());
        Assert.True(_sut.IsBlocked);
        Assert.NotEqual(string.Empty, _sut.BlockedReason);
    }

    [Fact]
    public void TheMusicStepIsPassableOnceAFolderExists()
    {
        var step = GoTo<MusicDirectoryStepViewModel>();

        // Named on the mock rather than relying on the real temp directory, which is not what the
        // step reads any more.
        _fileSystem.Directory.CreateDirectory("/music");
        step.MusicDirectoryPath = "/music";

        Assert.True(CanContinueNow());
    }

    [Fact]
    public void AFolderThatDoesNotExistIsNotAFolder()
    {
        var step = GoTo<MusicDirectoryStepViewModel>();

        step.MusicDirectoryPath = "/music/that/was/never/created";

        Assert.False(CanContinueNow());
    }

    [Fact]
    public void ProgressText_CountsTheSteps() => Assert.Equal("Step 1 of 5", _sut.ProgressText);

    [Fact]
    public void ContinueLabel_SaysFinishOnlyOnTheLastStep()
    {
        Assert.Equal("Next", _sut.ContinueLabel);

        _sut.ContinueCommand.Execute().Subscribe();
        Assert.Equal("Next", _sut.ContinueLabel);

        _sut.ContinueCommand.Execute().Subscribe();
        _sut.ContinueCommand.Execute().Subscribe();
        _sut.ContinueCommand.Execute().Subscribe();

        Assert.Equal("Finish", _sut.ContinueLabel);
    }

    [Fact]
    public void Continue_MovesToTheNextStep()
    {
        _sut.ContinueCommand.Execute().Subscribe();

        Assert.IsType<DanceListStepViewModel>(_sut.CurrentStep);
        Assert.False(_sut.IsLastStep);
    }

    [Fact]
    public void Back_ReturnsToThePreviousStep()
    {
        _sut.ContinueCommand.Execute().Subscribe();

        _sut.BackCommand.Execute().Subscribe();

        Assert.IsType<WelcomeStepViewModel>(_sut.CurrentStep);
    }

    [Fact]
    public void Back_IsUnavailableOnTheFirstStep()
    {
        var canGoBack = true;
        using var subscription = _sut.BackCommand.CanExecute.Subscribe(value => canGoBack = value);

        Assert.False(canGoBack);
    }

    [Fact]
    public void FinishingTheLastStep_MarksSetupCompleted()
    {
        RunToTheEnd();

        Assert.True(_settings.SetupCompleted);
    }

    [Fact]
    public void FinishingTheLastStep_WritesTheMusicDirectory()
    {
        _sut.ContinueCommand.Execute().Subscribe();
        _sut.ContinueCommand.Execute().Subscribe();
        ((MusicDirectoryStepViewModel)_sut.CurrentStep).MusicDirectoryPath = "/music";
        _sut.ContinueCommand.Execute().Subscribe();
        _sut.ContinueCommand.Execute().Subscribe();

        Assert.Equal("/music", _settings.MusicDirectoryPath);
    }

    [Fact]
    public void FinishingTheLastStep_Signals()
    {
        var finished = false;
        using var subscription = _sut.Finished.Subscribe(_ => finished = true);

        RunToTheEnd();

        Assert.True(finished);
    }

    [Fact]
    public void SetupIsNotCompletedBeforeTheLastStep()
    {
        _sut.ContinueCommand.Execute().Subscribe();

        Assert.False(_settings.SetupCompleted);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _danceListSubject.Dispose();
        _statusSubject.Dispose();
    }

    [Fact]
    public void GoingBackFromTheReviewStep_StopsThePreview()
    {
        GoTo<MusicDirectoryStepViewModel>().MusicDirectoryPath = Path.GetTempPath();
        GoTo<ReviewStepViewModel>();
        _preview.ClearReceivedCalls();

        _sut.BackCommand.Execute().Subscribe();

        Assert.IsNotType<ReviewStepViewModel>(_sut.CurrentStep);
        _preview.Received().StopAsync();
    }

    private T GoTo<T>() where T : WizardStepViewModel
    {
        for (var i = 0; i < _sut.Steps.Count && _sut.CurrentStep is not T; i++)
        {
            _sut.ContinueCommand.Execute().Subscribe();
        }

        return Assert.IsType<T>(_sut.CurrentStep);
    }

    private void RunToTheEnd()
    {
        for (var i = 0; i < _sut.Steps.Count; i++)
        {
            _sut.ContinueCommand.Execute().Subscribe();
        }
    }

    private bool CanContinueNow()
    {
        var canContinue = false;
        using var subscription = _sut.ContinueCommand.CanExecute.Subscribe(value => canContinue = value);
        return canContinue;
    }
}
