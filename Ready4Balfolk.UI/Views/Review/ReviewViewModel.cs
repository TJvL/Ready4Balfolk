using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Review;

/// <summary>The gate into the library, and the only way across it.</summary>
/// <remarks>
/// <para>
/// A fixture rather than a phase of setup. Retag a file, rename one, drop new ones in, and they come
/// back here on their own, because a value that was true about the old file is not a value anybody
/// agreed to about the new one.
/// </para>
/// <para>
/// The queue is over tracks and ordered least confident first, so stopping halfway through still
/// leaves the library better: whoever answers forty rows has answered the forty nothing could speak
/// for. It is keyboard driven for the same reason — 2668 mouse trips is the difference between an
/// evening and never.
/// </para>
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class ReviewViewModel : ReactiveObject, IDisposable
{
    private readonly ILibraryIndex _libraryIndex;
    private readonly IDanceListStore _danceListStore;
    private readonly ISettingsStore _settingsStore;
    private readonly ITrackStore _trackStore;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    public ReviewViewModel(
        ILibraryIndex libraryIndex,
        IDanceListStore danceListStore,
        ISettingsStore settingsStore,
        ITrackStore trackStore,
        ILoggerService loggerService)
    {
        _libraryIndex = libraryIndex;
        _danceListStore = danceListStore;
        _settingsStore = settingsStore;
        _trackStore = trackStore;
        _loggerService = loggerService;

        Summary = string.Empty;
        ScanProgressText = string.Empty;
        AllDances = [];

        // The queue waits for the scan and then builds itself. A first run reaches this screen while
        // thousands of files are still being read, and "nothing is waiting" over a half-read index
        // is not an empty queue, it is a lie about the library.
        trackStore.IsLoading
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(loading =>
            {
                IsScanning = loading;
                if (!loading)
                {
                    RefreshCommand.Execute().Subscribe();
                }
            })
            .DisposeWith(_disposables);

        // While it runs the only thing that moves is a count, read from the index rather than from
        // the library: nothing reaches the library during a scan, so counting that would sit at nil.
        this.WhenAnyValue(x => x.IsScanning)
            .Select(scanning => scanning
                ? Observable.Interval(TimeSpan.FromSeconds(1)).Select(_ => Unit.Default)
                : Observable.Empty<Unit>())
            .Switch()
            .SelectMany(_ => Observable.FromAsync(() => _libraryIndex.CountInReviewAsync()))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(count => ScanProgressText = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Review_Scanning, count))
            .DisposeWith(_disposables);
    }

    /// <summary>True while the library is being read, which on a first run is most of this screen.</summary>
    [Reactive] public partial bool IsScanning { get; private set; }

    /// <summary>How far the scan has got. The only thing that moves while it runs.</summary>
    [Reactive] public partial string ScanProgressText { get; private set; }

    [Reactive] public partial bool IsBusy { get; private set; }

    /// <summary>How many tracks are waiting, which is the whole of what this screen is about.</summary>
    [Reactive] public partial string Summary { get; private set; }

    [Reactive] public partial bool IsEmpty { get; private set; }

    /// <summary>
    /// The queue, flat, with each row knowing whether it opens a folder.
    /// </summary>
    /// <remarks>
    /// Flat rather than nested so that arrow keys walk the whole queue without anybody having to
    /// think about which list has focus, and so a folder header costs nothing to render.
    /// </remarks>
    public ObservableCollection<ReviewRowViewModel> Rows { get; } = [];

    [Reactive] public partial ReviewRowViewModel? Selected { get; set; }

    /// <summary>Every name the published list knows, for the picker. The only vocabulary there is.</summary>
    [Reactive] public partial IReadOnlyList<string> AllDances { get; private set; }

    /// <summary>Rebuilds the queue from the index, which is what makes it resumable.</summary>
    [ReactiveCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _libraryIndex.OpenAsync();
            var entries = await _libraryIndex.SnapshotByPathAsync();
            var approvals = await _libraryIndex.ApprovalsAsync();
            var dances = _danceListStore.Index;

            var groups = ReviewQueue.Build(entries, approvals, dances, _settingsStore.Current.MusicDirectoryPath);

            Rows.Clear();
            foreach (var group in groups)
            {
                var first = true;
                foreach (var track in group.Tracks)
                {
                    Rows.Add(new ReviewRowViewModel(track, first));
                    first = false;
                }
            }

            AllDances = [.. dances.Dances.Select(dance => dance.DisplayName).OrderBy(name => name, StringComparer.CurrentCulture)];
            Selected = Rows.FirstOrDefault();
            IsEmpty = Rows.Count == 0 && !IsScanning;
            Summary = Rows.Count == 0
                ? UiStrings.Review_NothingWaiting
                : string.Format(CultureInfo.CurrentCulture, UiStrings.Review_Waiting, Rows.Count, groups.Count);
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to build the review queue", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Answers one track: what is in its three boxes, agreed to individually.</summary>
    [ReactiveCommand]
    private async Task ApproveAsync(ReviewRowViewModel? row)
    {
        if (row is null || !row.CanApprove)
        {
            return;
        }

        await ApproveRowAsync(row);
        await _trackStore.RefreshLibraryAsync();

        Selected = NextAfter(row);
    }

    /// <summary>
    /// Answers every waiting track in the same folder at once.
    /// </summary>
    /// <remarks>
    /// A folder is where the remaining evidence is, and confirming one is the difference between an
    /// evening and never. It only takes the rows that can be answered as they stand: a row still
    /// missing a field is left where it is rather than being quietly approved as blank.
    /// </remarks>
    [ReactiveCommand]
    private async Task ApproveFolderAsync(ReviewRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        foreach (var sibling in Rows.Where(candidate =>
                     string.Equals(candidate.Folder, row.Folder, StringComparison.Ordinal)
                     && !candidate.IsApproved
                     && candidate.CanApprove))
        {
            await ApproveRowAsync(sibling);
        }

        await _trackStore.RefreshLibraryAsync();

        Selected = NextAfter(row);
    }

    public void Dispose() => _disposables.Dispose();

    private async Task ApproveRowAsync(ReviewRowViewModel row)
    {
        await _libraryIndex.ApproveIndividuallyAsync([row.Path], TrackField.Dance, row.Dance.Trim());
        await _libraryIndex.ApproveIndividuallyAsync([row.Path], TrackField.Artist, row.Artist.Trim());
        await _libraryIndex.ApproveIndividuallyAsync([row.Path], TrackField.Title, row.Title.Trim());

        // A dance the published list has never heard of is not a local problem to patch around. The
        // answer is kept, the track parks, and a proposal at BigBalfolkList is what releases it.
        var known = _danceListStore.Index.ResolveSlug(row.Dance.Trim()) is not null;
        row.MarkApproved(known);
    }

    /// <summary>The next row still waiting, so answering one lands on the next question.</summary>
    private ReviewRowViewModel? NextAfter(ReviewRowViewModel row)
    {
        var at = Rows.IndexOf(row);

        return Rows.Skip(at + 1).FirstOrDefault(candidate => !candidate.IsApproved)
            ?? Rows.FirstOrDefault(candidate => !candidate.IsApproved);
    }
}
