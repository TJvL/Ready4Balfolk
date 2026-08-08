using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.UI.Views.Wizard;

namespace Ready4Balfolk.Tests.ViewModels;

public sealed class SetupWizardViewModelTests : IDisposable
{
    private readonly IDanceListStore _danceListStore = Substitute.For<IDanceListStore>();
    private readonly BehaviorSubject<DanceList> _danceListSubject = new(DanceList.Empty);
    private readonly IConfirmationService _confirmations = Substitute.For<IConfirmationService>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly SetupWizardViewModel _sut;
    private ApplicationSettings _settings = new();

    public SetupWizardViewModelTests()
    {
        _danceListStore.Current.Returns(_ => _danceListSubject.Value);
        _danceListStore.Observe().Returns(_danceListSubject);
        _danceListStore.Index.Returns(_ => DanceListIndex.Build(_danceListSubject.Value));
        _danceListStore.IsLoading.Returns(Observable.Return(false));

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
        var editor = new DanceListViewModel(_danceListStore, Substitute.For<IEditorHistoryService>(),
            notifications, _confirmations, logger);

        return new SetupWizardViewModel(
            new DanceListStepViewModel(_danceListStore, logger, notifications, _confirmations),
            new DanceListEditStepViewModel(editor),
            new MusicDirectoryStepViewModel(_settingsStore),
            _settingsStore,
            logger);
    }

    [Fact]
    public void StartsOnTheDanceListStep()
    {
        Assert.IsType<DanceListStepViewModel>(_sut.CurrentStep);
        Assert.True(_sut.IsFirstStep);
        Assert.False(_sut.IsLastStep);
    }

    [Fact]
    public void ProgressText_CountsTheSteps() => Assert.Equal("Step 1 of 3", _sut.ProgressText);

    [Fact]
    public void ContinueLabel_SaysFinishOnlyOnTheLastStep()
    {
        Assert.Equal("Next", _sut.ContinueLabel);

        AnswerDanceListStep();
        _sut.ContinueCommand.Execute().Subscribe();
        Assert.Equal("Next", _sut.ContinueLabel);

        _sut.ContinueCommand.Execute().Subscribe();

        Assert.Equal("Finish", _sut.ContinueLabel);
    }

    [Fact]
    public void UnansweredDanceListStep_BlocksContinuing() => Assert.False(CanContinueNow());

    [Fact]
    public void AnsweredDanceListStep_AllowsContinuing()
    {
        AnswerDanceListStep();

        Assert.True(CanContinueNow());
    }

    [Fact]
    public void Continue_MovesToTheNextStep()
    {
        AnswerDanceListStep();

        _sut.ContinueCommand.Execute().Subscribe();

        Assert.IsType<DanceListEditStepViewModel>(_sut.CurrentStep);
        Assert.False(_sut.IsLastStep);
    }

    [Fact]
    public void CanContinue_FollowsTheStepThatIsShowing()
    {
        AnswerDanceListStep();
        _sut.ContinueCommand.Execute().Subscribe();

        // The music step has no answer to give and never blocks, so leaving the dance list step
        // must stop the dance list step's opinion from counting.
        Assert.True(CanContinueNow());
    }

    [Fact]
    public void Back_ReturnsToThePreviousStep()
    {
        AnswerDanceListStep();
        _sut.ContinueCommand.Execute().Subscribe();

        _sut.BackCommand.Execute().Subscribe();

        Assert.IsType<DanceListStepViewModel>(_sut.CurrentStep);
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
        AnswerDanceListStep();
        _sut.ContinueCommand.Execute().Subscribe();
        _sut.ContinueCommand.Execute().Subscribe();
        ((MusicDirectoryStepViewModel)_sut.CurrentStep).MusicDirectoryPath = "/music";
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
        AnswerDanceListStep();
        _sut.ContinueCommand.Execute().Subscribe();

        Assert.False(_settings.SetupCompleted);
    }

    [Fact]
    public void ExistingDanceList_CountsAsAlreadyAnswered()
    {
        _danceListSubject.OnNext(TestData.CreateSimpleDanceList());
        using var wizard = BuildWizard();

        var canContinue = false;
        using var subscription = wizard.ContinueCommand.CanExecute.Subscribe(value => canContinue = value);

        Assert.True(canContinue);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _danceListSubject.Dispose();
    }

    private void AnswerDanceListStep() =>
        ((DanceListStepViewModel)_sut.CurrentStep).StartEmptyCommand.Execute().Subscribe();

    private void RunToTheEnd()
    {
        AnswerDanceListStep();
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
