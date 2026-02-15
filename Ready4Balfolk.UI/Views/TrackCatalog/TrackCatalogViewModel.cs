using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.TrackCatalog;

public partial class TrackCatalogViewModel : ReactiveObject, IDisposable
{
    private readonly IQueueService _queueService;
    private readonly INotificationService _notificationService;
    private readonly CompositeDisposable _disposables = [];
    private readonly ReadOnlyObservableCollection<TrackViewModel> _tracks;

    public ReadOnlyObservableCollection<TrackViewModel> Tracks => _tracks;

    [Reactive] public partial string SearchText { get; set; }

    [ReactiveCommand]
    private void ClearSearch() => SearchText = "";

    [ReactiveCommand]
    private void EnqueueTrack(TrackViewModel track)
    {
        var result = _queueService.Enqueue(new TrackQueueItem(track.Track, RandomlyAdded: false));
        if (!result.Allowed)
            _notificationService.Show(result.RejectionReason!, NotificationSeverity.Warning);
    }

    public TrackCatalogViewModel(ITrackStore trackStore, IQueueService queueService,
        INotificationService notificationService)
    {
        _queueService = queueService;
        _notificationService = notificationService;
        SearchText = "";

        var searchObservable = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .DistinctUntilChanged();

        trackStore.Connect(searchObservable)
            .Transform(track => new TrackViewModel(track))
            .Sort(SortExpressionComparer<TrackViewModel>.Ascending(t => t.Dance))
            .ObserveOn(RxApp.MainThreadScheduler)
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
