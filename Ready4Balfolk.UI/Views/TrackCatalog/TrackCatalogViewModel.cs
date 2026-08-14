using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

#pragma warning disable CS8618 // ObservableAsProperty fields set by helpers in constructor
public partial class TrackCatalogViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueService _queueService;
    private readonly INotificationService _notificationService;
    private readonly TrackEditorService _trackEditor;
    private readonly CompositeDisposable _disposables = [];
    private readonly ReadOnlyObservableCollection<TrackViewModel> _tracks;

    public ReadOnlyObservableCollection<TrackViewModel> Tracks => _tracks;

    [ObservableAsProperty] public partial bool IsLoading { get; }

    [Reactive] public partial string SearchText { get; set; }

    /// <summary>The row the context menu acts on. The pencil passes its own row instead.</summary>
    [Reactive] public partial TrackViewModel? SelectedTrack { get; set; }

    [ReactiveCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>Opens the edit dialog for a row, so a typo is fixed the moment it is seen.</summary>
    [ReactiveCommand]
    private async Task EditTrackAsync(TrackViewModel? track)
    {
        var target = track ?? SelectedTrack;
        if (target is { } row)
        {
            await _trackEditor.EditAsync(row.Track);
        }
    }

    [ReactiveCommand]
    private void EnqueueTrack(TrackViewModel track)
    {
        var result = _queueService.Enqueue(new TrackQueueItem(track.Track, RandomlyAdded: false));
        if (!result.Allowed)
        {
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    public TrackCatalogViewModel(ITrackStore trackStore, IQueueService queueService,
        INotificationService notificationService, TrackEditorService trackEditor)
    {
        _queueService = queueService;
        _notificationService = notificationService;
        _trackEditor = trackEditor;
        SearchText = "";

        _isLoadingHelper = trackStore.IsLoading
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        var searchObservable = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .DistinctUntilChanged();

        trackStore.Connect(searchObservable)
            .Transform(track => new TrackViewModel(track))
            .Sort(SortExpressionComparer<TrackViewModel>.Ascending(t => t.Dance))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out _tracks)
            .Subscribe()
            .DisposeWith(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
