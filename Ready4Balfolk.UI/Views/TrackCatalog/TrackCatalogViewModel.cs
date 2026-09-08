using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
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

    /// <summary>
    /// What the empty grid means right now, or empty when there are rows to show.
    /// </summary>
    /// <remarks>
    /// A blank DataGrid looks the same whether nothing has been discovered yet, everything found is
    /// still waiting behind the review gate, or a search matched nothing: three different situations
    /// asking for three different sentences.
    /// </remarks>
    [ObservableAsProperty] public partial string EmptyStateText { get; }

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

    /// <summary>
    /// Takes back the answer a person gave a track, which puts it out of the library and back in
    /// review.
    /// </summary>
    /// <remarks>
    /// Here because this is where a track that has been through the gate still exists: the review
    /// queue is rebuilt from the index and drops everything already in the library, so the row that
    /// was answered is gone by the next scan and an answer given last week has nothing left to press
    /// a button on. Beside the pencil, because the two are the same question: this answer is wrong,
    /// and either I know the right one or I want to be asked again.
    /// </remarks>
    [ReactiveCommand]
    private async Task WithdrawTrackAsync(TrackViewModel? track)
    {
        if ((track ?? SelectedTrack) is not { } row)
        {
            return;
        }

        if (await _trackEditor.WithdrawAsync(row.Track))
        {
            _notificationService.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.TrackCatalog_WithdrawnTrack, row.Title),
                NotificationSeverity.Information);
            return;
        }

        // Nothing of this track was answered by hand, so nothing was taken back. Said out loud,
        // because a command that quietly does nothing reads as one that failed.
        _notificationService.Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.TrackCatalog_NothingToWithdraw, row.Title),
            NotificationSeverity.Warning);
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

    /// <remarks>
    /// <c>searchScheduler</c> is where the three tenths of a second between the last keystroke and
    /// the filter are counted. Real time unless a caller says otherwise, and only a test does:
    /// sleeping past a real throttle is the failure that passes on a quiet machine and fails on a
    /// busy one.
    /// </remarks>
    public TrackCatalogViewModel(ITrackStore trackStore, IQueueService queueService,
        INotificationService notificationService, TrackEditorService trackEditor,
        ISettingsStore settingsStore, IScheduler? searchScheduler = null)
    {
        _queueService = queueService;
        _notificationService = notificationService;
        _trackEditor = trackEditor;
        SearchText = "";

        _isLoadingHelper = trackStore.IsLoading
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        // Shared, so the filter and the sentence below react to one and the same typed term at one
        // and the same moment instead of running two throttles that can land in either order.
        var searchObservable = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300), searchScheduler ?? DefaultScheduler.Instance)
            .DistinctUntilChanged()
            .Publish()
            .RefCount();

        // How many rows the grid is showing, which is the one part of the empty state that the
        // grid itself reports.
        var rowCount = new BehaviorSubject<int>(0);

        trackStore.Connect(searchObservable)
            .Transform(track => new TrackViewModel(track))
            .Sort(SortExpressionComparer<TrackViewModel>.Ascending(t => t.Dance))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Bind(out _tracks)
            .Subscribe(_ => rowCount.OnNext(_tracks.Count))
            .DisposeWith(_disposables);

        // After the subscription that feeds it, so shutting the screen down stops the rows being
        // counted before the thing counting them is gone.
        rowCount.DisposeWith(_disposables);

        // Recomputed from every input it reads, rather than from the grid's changeset alone:
        // filtering an already empty result to empty again moves no row, so the grid publishes
        // nothing, and that is exactly the first-run case where the DJ searches a catalog the
        // review gate is holding everything back from.
        _emptyStateTextHelper = rowCount
            .CombineLatest(
                searchObservable.StartWith(SearchText),
                trackStore.InReviewCount,
                settingsStore.Observe()
                    .Select(settings => settings.MusicDirectoryPath)
                    .StartWith(settingsStore.Current.MusicDirectoryPath),
                DescribeEmptyGrid)
            .DistinctUntilChanged(StringComparer.Ordinal)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.EmptyStateText);
        _emptyStateTextHelper.DisposeWith(_disposables);
    }

    /// <summary>
    /// The sentence for a blank grid, or nothing at all once there is a row to show.
    /// </summary>
    /// <remarks>
    /// Order matters: a search is the DJ's own doing and gets its own answer regardless of what
    /// else is true, an unset music folder is the one nothing else can explain, and only after both
    /// of those is the review count the right thing to blame for an otherwise-empty library.
    /// </remarks>
    private static string DescribeEmptyGrid(
        int rowCount, string searchText, int inReviewCount, string musicDirectoryPath) =>
        (HasRows: rowCount > 0, IsSearching: searchText.Length > 0,
            HasFolder: musicDirectoryPath.Length > 0, IsWaiting: inReviewCount > 0) switch
        {
            { HasRows: true } => "",
            { IsSearching: true } => UiStrings.TrackCatalog_EmptySearch,
            { HasFolder: false } => UiStrings.TrackCatalog_EmptyNoFolder,
            { IsWaiting: true } => string.Format(
                CultureInfo.CurrentCulture, UiStrings.TrackCatalog_EmptyReviewWaiting, inReviewCount),
            _ => UiStrings.TrackCatalog_EmptyNoTracks,
        };

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
