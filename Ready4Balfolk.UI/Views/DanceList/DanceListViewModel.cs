using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
// The view namespace is also called DanceList, so the model needs a name of its own here.
using DanceListModel = Ready4Balfolk.Domain.Models.Dances.DanceList;

namespace Ready4Balfolk.UI.Views.DanceList;

/// <summary>The dance panel: the published list, browsed by tag and by spelling.</summary>
/// <remarks>
/// <para>
/// The list is BigBalfolkList's, taken as published, so nothing here edits anything. What the panel
/// is for is choosing: the tags picked in the rail are the pool a random pick and the auto-queue
/// draw from, and the dice on a card asks for that one dance.
/// </para>
/// <para>
/// The pool is a union, not an intersection. One control cannot mean both "show me dances that are
/// Breton and a gavotte" and "draw from Breton dances and waltzes", and the pool is the one this
/// panel exists to set.
/// </para>
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class DanceListViewModel : ReactiveObject, IDisposable
{
    private readonly IDanceListStore _store;
    private readonly IDancePool _pool;
    private readonly ITrackStore _trackStore;
    private readonly IRandomTrackService _randomTrackService;
    private readonly IQueueService _queueService;
    private readonly INotificationService _notifications;
    private readonly IDanceListFeed _feed;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    [Reactive] public partial string SearchText { get; set; }

    [Reactive] public partial IReadOnlyList<TagChipViewModel> Tags { get; private set; }

    [Reactive] public partial IReadOnlyList<DanceCardViewModel> Dances { get; private set; }

    /// <summary>What a random pick is scoped to, said out loud.</summary>
    [Reactive] public partial string PoolDescription { get; private set; }

    [Reactive] public partial bool HasPool { get; private set; }

    /// <summary>How many dances are showing, and how many the list has in total.</summary>
    [Reactive] public partial string SummaryText { get; private set; }

    /// <summary>Where the list came from and when, so a stale one is visible rather than assumed.</summary>
    [Reactive] public partial string OriginText { get; private set; }

    [Reactive] public partial bool IsUpdating { get; private set; }

    [ObservableAsProperty] public partial bool IsLoading { get; }

    public Uri SourceUri => _feed.HomePage;

    public DanceListViewModel(
        IDanceListStore store,
        IDancePool pool,
        ITrackStore trackStore,
        IRandomTrackService randomTrackService,
        IQueueService queueService,
        INotificationService notifications,
        IDanceListFeed feed,
        ILoggerService loggerService)
    {
        _store = store;
        _pool = pool;
        _trackStore = trackStore;
        _randomTrackService = randomTrackService;
        _queueService = queueService;
        _notifications = notifications;
        _feed = feed;
        _loggerService = loggerService;

        SearchText = string.Empty;
        Tags = [];
        Dances = [];
        PoolDescription = UiStrings.DanceList_PoolEverything;
        SummaryText = string.Empty;
        OriginText = string.Empty;

        _isLoadingHelper = store.IsLoading
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .ToProperty(this, x => x.IsLoading);
        _isLoadingHelper.DisposeWith(_disposables);

        // The rail and the cards are rebuilt from whichever of the four changed: the list itself,
        // the pool, what the user typed, or the tracks that decide a card's count.
        var lists = store.Observe();
        var pools = pool.Observe();
        var searches = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(150), RxSchedulers.TaskpoolScheduler)
            .StartWith(string.Empty);
        var tracks = trackStore.Connect()
            .Throttle(TimeSpan.FromMilliseconds(250), RxSchedulers.TaskpoolScheduler)
            .Select(_ => System.Reactive.Unit.Default)
            .StartWith(System.Reactive.Unit.Default);

        lists.CombineLatest(pools, searches, tracks, (list, selection, search, _) => (list, selection, search))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(x => Rebuild(x.list, x.selection, x.search))
            .DisposeWith(_disposables);

        store.ObserveStatus()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(DescribeOrigin)
            .DisposeWith(_disposables);
    }

    /// <summary>Puts a tag in the pool, or takes it out again.</summary>
    [ReactiveCommand]
    private void ToggleTag(string tag) => _pool.Toggle(tag);

    /// <summary>Back to drawing from everything.</summary>
    [ReactiveCommand]
    private void ClearPool() => _pool.Clear();

    /// <summary>Queues a random track of one named dance, whatever the pool happens to be.</summary>
    [ReactiveCommand]
    private void PickDance(string slug)
    {
        var track = _randomTrackService.PickRandomTrack(
            new RandomSelectionScope.SingleDance(slug), allowDuplicates: false);
        if (track is null)
        {
            _notifications.Show(
                string.Format(
                    CultureInfo.CurrentCulture,
                    UiStrings.DanceList_NoTrackForDance,
                    _store.Index.DisplayNameFor(slug)),
                NotificationSeverity.Warning);
            return;
        }

        var result = _queueService.Enqueue(new TrackQueueItem(track, RandomlyAdded: true));
        if (!result.Allowed)
        {
            _notifications.Show(result.RejectionReason!, NotificationSeverity.Warning);
        }
    }

    /// <summary>Asks BigBalfolkList for a newer list.</summary>
    [ReactiveCommand]
    private async Task UpdateAsync()
    {
        IsUpdating = true;
        try
        {
            Report(await _store.RefreshAsync());
        }
        finally
        {
            IsUpdating = false;
        }
    }

    /// <summary>Takes a list from a file, for a machine that never reaches the internet.</summary>
    public async Task UpdateFromFileAsync(FileInfo sourceFileInfo)
    {
        IsUpdating = true;
        try
        {
            Report(await _store.UpdateFromFileAsync(sourceFileInfo));
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to update the dance list from a file", exception);
            _notifications.Show(
                string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, exception.Message),
                NotificationSeverity.Error);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    public void Dispose() => _disposables.Dispose();

    private void Report(DanceListUpdate update)
    {
        switch (update.Outcome)
        {
            case DanceListUpdateOutcome.Updated:
                _notifications.Show(
                    string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_Updated, update.DancesAdded),
                    NotificationSeverity.Information);
                break;

            case DanceListUpdateOutcome.AlreadyCurrent:
                _notifications.Show(UiStrings.DanceList_AlreadyCurrent, NotificationSeverity.Information);
                break;

            case DanceListUpdateOutcome.Failed:
            default:
                // Not an error state: the list already in hand carries on working.
                _notifications.Show(
                    string.Format(CultureInfo.CurrentCulture, UiStrings.DanceList_UpdateFailed, update.Problem),
                    NotificationSeverity.Warning);
                break;
        }
    }

    private void Rebuild(DanceListModel list, DancePoolSelection selection, string search)
    {
        var folded = StringNormalizer.Normalize(search);
        var inPool = selection.Tags.ToHashSet(StringComparer.Ordinal);
        var excluded = selection.ExcludedTags.ToHashSet(StringComparer.Ordinal);

        var trackCounts = _trackStore.Current
            .Where(track => track.DanceSlug is not null)
            .GroupBy(track => track.DanceSlug!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        // The pool narrows what is shown as well as what is drawn, so the panel is never claiming
        // to draw from something you cannot see. An exclusion beats an inclusion, here as in the
        // draw itself.
        var matching = list.WithAnyTag(selection.Tags)
            .Where(dance => !excluded.Any(dance.HasTag))
            .Where(dance => Matches(dance, folded))
            .ToList();

        Dances =
        [
            .. matching
                .OrderBy(dance => dance.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(dance => new DanceCardViewModel(dance, trackCounts.GetValueOrDefault(dance.Slug)))
        ];

        var reachable = matching.SelectMany(dance => dance.Tags).ToHashSet(StringComparer.Ordinal);
        var counts = list.Tags.ToDictionary(tag => tag, list.CountOf, StringComparer.Ordinal);
        var largest = counts.Count == 0 ? 1 : counts.Values.Max();

        Tags =
        [
            .. list.Tags
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .Select(tag => new TagChipViewModel(
                    tag, counts[tag], largest, inPool.Contains(tag), excluded.Contains(tag), reachable.Contains(tag)))
        ];

        HasPool = !selection.IsEverything;
        var drawable = list.WithAnyTag(selection.Tags).Count(dance => !excluded.Any(dance.HasTag));
        var description = selection.Tags.Count == 0 && excluded.Count == 0
            ? UiStrings.DanceList_PoolEverything
            : string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.DanceList_PoolFormat,
                selection.Tags.Count == 0
                    ? UiStrings.DanceList_PoolAnyTag
                    : string.Join(", ", selection.Tags),
                drawable);
        PoolDescription = excluded.Count == 0
            ? description
            : description + string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_PoolNever, string.Join(", ", selection.ExcludedTags));

        SummaryText = string.Format(
            CultureInfo.CurrentCulture, UiStrings.DanceList_Summary, Dances.Count, list.Dances.Count);
    }

    private static bool Matches(Dance dance, string foldedSearch) =>
        foldedSearch.Length == 0
        || dance.Names.Any(name =>
            StringNormalizer.Normalize(name).Contains(foldedSearch, StringComparison.Ordinal));

    private void DescribeOrigin(DanceListStatus status) =>
        OriginText = status.ObtainedAt is { } obtainedAt
            ? string.Format(
                CultureInfo.CurrentCulture, UiStrings.DanceList_Obtained, obtainedAt.ToLocalTime().DateTime)
            : UiStrings.DanceList_ObtainedBuiltIn;
}
