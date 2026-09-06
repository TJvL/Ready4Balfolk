using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using DynamicData;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

/// <summary>The account of an evening: tonight's, or one that has already been filed.</summary>
/// <remarks>
/// A night that ends is kept rather than lost, so this screen chooses which night it is showing.
/// Before that the tab emptied itself the moment the closing song finished, which is the one moment
/// somebody wants to read back what was played.
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields set by helpers in constructor
public sealed partial class HistoryViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueHistoryStore _historyStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IConfirmationService _confirmationService;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private readonly SourceList<HistoryItemViewModel> _sourceList = new();
    private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _items;

    /// <summary>True while the night list is being rebuilt, so its own writes are not read back.</summary>
    private bool _rebuilding;

    /// <summary>Whether somebody has picked a night themselves, which nothing may then override.</summary>
    private bool _chosenByHand;

    private long _tonightId;

    public ReadOnlyObservableCollection<HistoryItemViewModel> Items => _items;

    /// <summary>Every night there is to look at, tonight first.</summary>
    public ObservableCollection<NightOption> Nights { get; } = [];

    [ObservableAsProperty] public partial bool IsLoading { get; }

    [Reactive] public partial NightOption? SelectedNight { get; set; }

    [Reactive] public partial string ItemCountText { get; set; }

    /// <summary>Which night is on screen, so a list of entries is never anonymous.</summary>
    [Reactive] public partial string ShowingText { get; set; }

    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool HasItems { get; set; }

    /// <summary>Whether tonight has anything in it, which is the only night that can be filed.</summary>
    [Reactive] public partial bool CanEndTonight { get; set; }

    private IObservable<bool> HasANight => this.WhenAnyValue(x => x.HasItems);

    private IObservable<bool> TonightHasSomethingInIt => this.WhenAnyValue(x => x.CanEndTonight);

    /// <remarks>
    /// Named from the side somebody is standing on. The soundcheck is the real case: three tracks
    /// played while testing the speakers at seven o'clock have already opened a night, and what is
    /// wanted then is to start again rather than to end anything. It keeps a confirmation all the
    /// same, because pressing it mid-set splits one evening into two.
    /// </remarks>
    [ReactiveCommand(CanExecute = nameof(TonightHasSomethingInIt))]
    private async Task StartNewNight()
    {
        if (!await _confirmationService.ConfirmAsync(UiStrings.HistoryToolbar_NewNightTitle, UiStrings.HistoryToolbar_NewNightMessage, UiStrings.HistoryToolbar_NewNightButton, UiStrings.HistoryToolbar_CancelButton, ConfirmationStakes.Destructive))
        {
            return;
        }

        await _historyStore.EndNightAsync();
    }

    /// <summary>Throws away the night that is being looked at, which is not always tonight.</summary>
    [ReactiveCommand(CanExecute = nameof(HasANight))]
    private async Task DeleteNight()
    {
        if (SelectedNight is not { } night)
        {
            return;
        }

        if (!await _confirmationService.ConfirmAsync(UiStrings.HistoryToolbar_DeleteNightTitle, UiStrings.HistoryToolbar_DeleteNightMessage, UiStrings.HistoryToolbar_DeleteButton, UiStrings.HistoryToolbar_CancelButton, ConfirmationStakes.Destructive))
        {
            return;
        }

        await _historyStore.DeleteNightAsync(IdOf(night));
        await RefreshNightsAsync();
    }

    /// <summary>Writes out the night that is being looked at, filed or not.</summary>
    public async Task ExportAsync(string path)
    {
        if (SelectedNight is { } night)
        {
            await _historyStore.ExportAsync(IdOf(night), path);
        }
    }

    public HistoryViewModel(
        IQueueHistoryStore historyStore,
        ISettingsStore settingsStore,
        IConfirmationService confirmationService,
        ILoggerService loggerService)
    {
        _historyStore = historyStore;
        _settingsStore = settingsStore;
        _confirmationService = confirmationService;
        _loggerService = loggerService;
        ItemCountText = UiStrings.History_NoHistory;
        ShowingText = UiStrings.History_Tonight;

        // Tonight is there from the start, before anything has been read off the file: the screen
        // opens on the evening that is running, and that is also the night the buttons act on.
        Nights.Add(NightOption.Tonight(0));
        SelectedNight = Nights[0];

        _isLoadingHelper = historyStore.IsLoading
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        _sourceList.Connect()
            .Bind(out _items)
            .Subscribe()
            .DisposeWith(_disposables);

        historyStore.Observe()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(OnTonightChanged)
            .DisposeWith(_disposables);

        // A template edited in the settings is on screen at once: the rows hold rendered text, so
        // they are built again rather than waiting for the next thing to happen in the evening.
        settingsStore.Observe()
            .Select(settings => settings.DisplayTemplates.HistoryItem)
            .DistinctUntilChanged(StringComparer.Ordinal)
            .Skip(1)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => ShowAsync(SelectedNight).SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to redraw a night", exception)))
            .DisposeWith(_disposables);

        // The nights on file are read once the store has them. A machine that was shut down after
        // the closing song has no running night at all, so nothing else would ever ask, and the
        // screen would open empty over a database full of evenings.
        historyStore.IsLoading
            .Where(loading => !loading)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RefreshNightsAsync().SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to read the nights on file", exception)))
            .DisposeWith(_disposables);

        // The night being looked at is read once and then stands still: only tonight changes while
        // somebody is reading it.
        this.WhenAnyValue(x => x.SelectedNight)
            .Skip(1)
            .Where(_ => !_rebuilding)
            .Subscribe(night =>
            {
                _chosenByHand = true;
                ShowAsync(night).SafeFireAndForget(exception =>
                    _loggerService.ErrorAsync("Failed to show a night", exception));
            })
            .DisposeWith(_disposables);

        _sourceList.DisposeWith(_disposables);
    }

    /// <summary>Reads the nights on file, for the first look at this screen.</summary>
    public async Task RefreshNightsAsync()
    {
        var nights = await _historyStore.ListNightsAsync();
        var wanted = SelectedNight;

        _rebuilding = true;
        try
        {
            Nights.Clear();
            Nights.Add(NightOption.Tonight(_historyStore.Current.Id));

            foreach (var night in nights.Where(night => !night.IsOpen))
            {
                Nights.Add(NightOption.For(night));
            }

            // The same night stays selected across a rebuild, so a track finishing does not move
            // somebody off the evening they were reading. Tonight is matched as tonight rather than
            // by its id, which it does not have until something happens in it.
            SelectedNight = wanted is { IsTonight: false }
                ? Nights.FirstOrDefault(option => option.Id == wanted.Id) ?? Nights[0]
                : Nights[0];

            // Nobody has chosen yet and tonight is empty, so the last evening is what this screen
            // is for: the alternative is a blank list under the words "no history" on a machine
            // that holds a season of dancing.
            if (!_chosenByHand && _historyStore.Current.Entries.Count == 0 && Nights.Count > 1)
            {
                SelectedNight = Nights[1];
            }
        }
        finally
        {
            _rebuilding = false;
        }

        await ShowAsync(SelectedNight);
    }

    /// <summary>Tonight, as it happens. A filed night on screen is left where it is.</summary>
    private void OnTonightChanged(QueueHistory tonight)
    {
        CanEndTonight = tonight.Entries.Count > 0;

        // The night was filed, by the closing song or by a person. What was on screen is now an
        // evening on file rather than nothing at all, so it is followed there instead of vanishing.
        if (tonight.Id == 0 && _tonightId != 0)
        {
            var filed = _tonightId;
            _tonightId = 0;
            FollowFiledNightAsync(filed).SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to follow a night that was filed", exception));
            return;
        }

        // A night opening is a night the list does not have yet.
        var opened = tonight.Id != 0 && tonight.Id != _tonightId;
        _tonightId = tonight.Id;

        if (SelectedNight is null or { IsTonight: true })
        {
            Show(tonight, UiStrings.History_Tonight);
        }

        if (opened)
        {
            RefreshNightsAsync().SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to read the nights on file", exception));
        }
    }

    private async Task FollowFiledNightAsync(long nightId)
    {
        var wasReadingTonight = SelectedNight is null or { IsTonight: true };
        await RefreshNightsAsync();

        if (wasReadingTonight && Nights.FirstOrDefault(option => option.Id == nightId) is { } filed)
        {
            SelectedNight = filed;
        }
    }

    /// <summary>Which night this option means now: tonight's id moves as the evening opens.</summary>
    private long IdOf(NightOption night) => night.IsTonight ? _historyStore.Current.Id : night.Id;

    private async Task ShowAsync(NightOption? night)
    {
        if (night is null)
        {
            Show(QueueHistory.Empty, UiStrings.History_Tonight);
            return;
        }

        if (night.IsTonight)
        {
            Show(_historyStore.Current, night.Label);
            return;
        }

        Show(await _historyStore.ReadNightAsync(night.Id) ?? QueueHistory.Empty, night.Label);
    }

    private void Show(QueueHistory history, string label)
    {
        _sourceList.Edit(list =>
        {
            list.Clear();

            // The night begins where the first thing in it does, so an evening nothing has happened
            // in yet is genuinely empty rather than a heading over nothing.
            if (history.StartedAt is { } startedAt && history.Entries.Count > 0)
            {
                list.Add(HistoryItemViewModel.ForNightStart(startedAt));
            }

            var template = _settingsStore.Current.DisplayTemplates.HistoryItem;
            list.AddRange(history.Entries.Select(entry => HistoryItemViewModel.ForEntry(entry, template)));

            if (history.EndedAt is { } endedAt)
            {
                list.Add(HistoryItemViewModel.ForNightEnd(endedAt));
            }
        });

        HasItems = history.Entries.Count > 0;
        ShowingText = label;
        ItemCountText = history.Entries.Count switch
        {
            0 => UiStrings.History_NoHistory,
            1 => string.Format(CultureInfo.CurrentCulture, UiStrings.History_ItemCount, 1),
            var count => string.Format(CultureInfo.CurrentCulture, UiStrings.History_ItemCountPlural, count)
        };
    }

    public void Dispose() => _disposables.Dispose();
}
