using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Wizard;

/// <summary>Drives the first-run wizard: which step is showing, and whether it may be left.</summary>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class SetupWizardViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<Unit> _finished = new();

    [Reactive] public partial int CurrentIndex { get; private set; }

    /// <summary>True while a step's commit is running, so a second click cannot run it twice.</summary>
    [Reactive] public partial bool IsBusy { get; private set; }

    [ObservableAsProperty] public partial WizardStepViewModel CurrentStep { get; }
    [ObservableAsProperty] public partial string ProgressText { get; }
    [ObservableAsProperty] public partial bool IsFirstStep { get; }
    [ObservableAsProperty] public partial bool IsLastStep { get; }
    [ObservableAsProperty] public partial string ContinueLabel { get; }

    public IReadOnlyList<WizardStepViewModel> Steps { get; }

    /// <summary>Fires once, when the last step has been committed. The window closes on it.</summary>
    public IObservable<Unit> Finished => _finished.AsObservable();

    public SetupWizardViewModel(
        DanceListStepViewModel danceListStep,
        DanceListEditStepViewModel danceListEditStep,
        MusicDirectoryStepViewModel musicDirectoryStep,
        ISettingsStore settingsStore,
        ILoggerService loggerService)
    {
        _settingsStore = settingsStore;
        _loggerService = loggerService;

        // The dance list comes first deliberately: it is what everything else in the application
        // then has something to say about. Editing it is a step of its own rather than something to
        // discover later, because an imported list is somebody else's answer and arrives needing work.
        Steps = [danceListStep, danceListEditStep, musicDirectoryStep];

        _currentStepHelper = this.WhenAnyValue(x => x.CurrentIndex)
            .Select(index => Steps[Math.Clamp(index, 0, Steps.Count - 1)])
            .ToProperty(this, x => x.CurrentStep);
        _currentStepHelper.DisposeWith(_disposables);

        _progressTextHelper = this.WhenAnyValue(x => x.CurrentIndex)
            .Select(index => string.Format(
                CultureInfo.CurrentCulture, UiStrings.Wizard_StepFormat, index + 1, Steps.Count))
            .ToProperty(this, x => x.ProgressText);
        _progressTextHelper.DisposeWith(_disposables);

        _isFirstStepHelper = this.WhenAnyValue(x => x.CurrentIndex)
            .Select(index => index == 0)
            .ToProperty(this, x => x.IsFirstStep);
        _isFirstStepHelper.DisposeWith(_disposables);

        _isLastStepHelper = this.WhenAnyValue(x => x.CurrentIndex)
            .Select(index => index == Steps.Count - 1)
            .ToProperty(this, x => x.IsLastStep);
        _isLastStepHelper.DisposeWith(_disposables);

        _continueLabelHelper = this.WhenAnyValue(x => x.IsLastStep)
            .Select(last => last ? UiStrings.Wizard_Finish : UiStrings.Wizard_Next)
            .ToProperty(this, x => x.ContinueLabel);
        _continueLabelHelper.DisposeWith(_disposables);

        Steps[0].EnterAsync().SafeFireAndForget(
            exception => _loggerService.ErrorAsync("Failed to enter the first setup step", exception));
    }

    /// <summary>
    /// The current step's own verdict, re-subscribed on every step change. Switch rather than Merge:
    /// the previous step's opinion must stop counting the moment it is left.
    /// </summary>
    private IObservable<bool> CanGoForward =>
        this.WhenAnyValue(x => x.CurrentStep)
            .Select(step => step.CanContinue)
            .Switch()
            .CombineLatest(this.WhenAnyValue(x => x.IsBusy), (can, busy) => can && !busy);

    private IObservable<bool> CanGoBack =>
        this.WhenAnyValue(x => x.IsFirstStep, x => x.IsBusy, (first, busy) => !first && !busy);

    [ReactiveCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            EnterCurrentStep();
        }
    }

    [ReactiveCommand(CanExecute = nameof(CanGoForward))]
    private async Task ContinueAsync()
    {
        IsBusy = true;
        try
        {
            if (!await CurrentStep.CommitAsync())
            {
                return;
            }

            if (!IsLastStep)
            {
                CurrentIndex++;
                await CurrentStep.EnterAsync();
                return;
            }

            await _settingsStore.UpdateAsync(settings => settings with { SetupCompleted = true });
            await _loggerService.InfoAsync("Setup wizard completed");
            _finished.OnNext(Unit.Default);
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Setup wizard step failed", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _finished.Dispose();
    }

    private void EnterCurrentStep() =>
        CurrentStep.EnterAsync().SafeFireAndForget(
            exception => _loggerService.ErrorAsync("Failed to enter a setup step", exception));
}
