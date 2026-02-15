using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Stores.History;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.History;

public sealed partial class HistoryViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueHistoryStore _historyStore;
    private readonly IConfirmationService _confirmationService;
    private readonly CompositeDisposable _disposables = [];
    private readonly SourceList<HistoryItemViewModel> _sourceList = new();
    private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _items;

    public ReadOnlyObservableCollection<HistoryItemViewModel> Items => _items;

    [Reactive] public partial string ItemCountText { get; set; }
    [Reactive] public partial string TotalDurationText { get; set; }

    // ReSharper disable once MemberCanBePrivate.Global
    [Reactive] public partial bool HasItems { get; set; }

    private IObservable<bool> CanClearHistory => this.WhenAnyValue(x => x.HasItems);

    [ReactiveCommand(CanExecute = nameof(CanClearHistory))]
    private async Task ClearHistory()
    {
        if (!await _confirmationService.ConfirmAsync("Clear History", "Remove all history entries?", "Clear", "Cancel"))
            return;

        await _historyStore.ClearAsync();
    }

    public async Task ExportAsync(FileInfo file) => await _historyStore.ExportAsync(file);

    public HistoryViewModel(IQueueHistoryStore historyStore, IConfirmationService confirmationService)
    {
        _historyStore = historyStore;
        _confirmationService = confirmationService;
        ItemCountText = "No history";
        TotalDurationText = "";

        _sourceList.Connect()
            .Bind(out _items)
            .Subscribe()
            .DisposeWith(_disposables);

        historyStore.Observe()
            .ObserveOn(RxApp.MainThreadScheduler)
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
            ItemCountText = "No history";
            TotalDurationText = "";
            return;
        }

        ItemCountText = $"{history.Entries.Count} item{(history.Entries.Count != 1 ? "s" : "")}";
        var totalDuration = history.TotalDuration;
        TotalDurationText = $"{(int)totalDuration.TotalMinutes}:{totalDuration.Seconds:D2}";
    }

    public void Dispose() => _disposables.Dispose();
}
