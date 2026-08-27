using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

#pragma warning disable CS8618 // ObservableAsProperty fields set by helpers in constructor
public sealed partial class HistoryViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueHistoryStore _historyStore;
    private readonly IConfirmationService _confirmationService;
    private readonly CompositeDisposable _disposables = [];
    private readonly SourceList<HistoryItemViewModel> _sourceList = new();
    private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _items;

    public ReadOnlyObservableCollection<HistoryItemViewModel> Items => _items;

    [ObservableAsProperty] public partial bool IsLoading { get; }

    [Reactive] public partial string ItemCountText { get; set; }
    [Reactive] public partial string TotalDurationText { get; set; }

    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool HasItems { get; set; }

    private IObservable<bool> HasANight => this.WhenAnyValue(x => x.HasItems);

    /// <remarks>
    /// Named from the side somebody is standing on. The soundcheck is the real case: three tracks
    /// played while testing the speakers at seven o'clock have already opened a night, and what is
    /// wanted then is to start again rather than to end anything. It keeps a confirmation all the
    /// same, because pressing it mid-set splits one evening into two.
    /// </remarks>
    [ReactiveCommand(CanExecute = nameof(HasANight))]
    private async Task StartNewNight()
    {
        if (!await _confirmationService.ConfirmAsync(UiStrings.HistoryToolbar_NewNightTitle, UiStrings.HistoryToolbar_NewNightMessage, UiStrings.HistoryToolbar_NewNightButton, UiStrings.HistoryToolbar_CancelButton))
        {
            return;
        }

        await _historyStore.EndNightAsync();
    }

    [ReactiveCommand(CanExecute = nameof(HasANight))]
    private async Task DeleteNight()
    {
        if (!await _confirmationService.ConfirmAsync(UiStrings.HistoryToolbar_DeleteNightTitle, UiStrings.HistoryToolbar_DeleteNightMessage, UiStrings.HistoryToolbar_DeleteButton, UiStrings.HistoryToolbar_CancelButton))
        {
            return;
        }

        await _historyStore.DeleteNightAsync();
    }

    public async Task ExportAsync(string path) => await _historyStore.ExportAsync(path);

    public HistoryViewModel(IQueueHistoryStore historyStore, IConfirmationService confirmationService)
    {
        _historyStore = historyStore;
        _confirmationService = confirmationService;
        ItemCountText = UiStrings.History_NoHistory;
        TotalDurationText = "";

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
            .Subscribe(OnHistoryChanged)
            .DisposeWith(_disposables);

        _sourceList.DisposeWith(_disposables);
    }

    private void OnHistoryChanged(QueueHistory history)
    {
        _sourceList.Edit(list =>
        {
            list.Clear();
            list.AddRange(history.Entries.Select(e => new HistoryItemViewModel(e)));
        });

        HasItems = history.Entries.Count > 0;
        UpdateStatusText(history);
    }

    private void UpdateStatusText(QueueHistory history)
    {
        if (history.Entries.Count == 0)
        {
            ItemCountText = UiStrings.History_NoHistory;
            TotalDurationText = "";
            return;
        }

        ItemCountText = history.Entries.Count == 1
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.History_ItemCount, history.Entries.Count)
            : string.Format(CultureInfo.CurrentCulture, UiStrings.History_ItemCountPlural, history.Entries.Count);

        var totalDuration = history.TotalDuration;
        TotalDurationText = $"{(int)totalDuration.TotalMinutes}:{totalDuration.Seconds:D2}";
    }

    public void Dispose() => _disposables.Dispose();
}
