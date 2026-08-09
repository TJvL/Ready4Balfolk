using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Tagging;

/// <summary>The report a scan produces, and the place its questions get answered.</summary>
/// <remarks>
/// One report, not a message per file. The unit of work is the distinct value rather than the track,
/// because twenty-one files claiming the same thing are one decision and presenting them as
/// twenty-one is how a list becomes one nobody ever finishes.
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class TaggingViewModel : ReactiveObject, IDisposable
{
    private readonly ILibraryIndex _libraryIndex;
    private readonly IDanceListStore _danceListStore;
    private readonly IPreviewPlaybackService _preview;
    private readonly INotificationService _notifications;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    [Reactive] public partial bool IsBusy { get; private set; }

    /// <summary>
    /// True while the library is still being read, which on a first run is most of the time the
    /// wizard is on this step.
    /// </summary>
    /// <remarks>
    /// Without this the report is built from a half-filled index and reads as though the library
    /// were mostly fine, which is the opposite of the truth and the worst possible first impression.
    /// </remarks>
    [Reactive] public partial bool IsScanning { get; private set; }

    /// <summary>How far the scan has got. The only thing that moves while it runs.</summary>
    [Reactive] public partial string ScanProgressText { get; private set; }

    /// <summary>Values that look like a misspelling of one dance, so one decision settles them.</summary>
    [Reactive] public partial IReadOnlyList<UnrecognisedValueViewModel> Suggestions { get; private set; }

    /// <summary>Values on several tracks that cannot be answered wholesale.</summary>
    [Reactive] public partial IReadOnlyList<UnrecognisedValueViewModel> Unrecognised { get; private set; }

    /// <summary>Values on a single track, kept apart so they do not bury the rest.</summary>
    [Reactive] public partial IReadOnlyList<UnrecognisedValueViewModel> OneOffs { get; private set; }

    /// <summary>Every dance in the list, for answering a single track that nothing suggested.</summary>
    [Reactive] public partial IReadOnlyList<DanceSuggestionRow> AllDances { get; private set; }

    [Reactive] public partial string SummaryText { get; private set; }

    [Reactive] public partial bool HasAnythingToReport { get; private set; }

    /// <summary>
    /// True only once the scan is over and there is genuinely nothing left. Mid-scan the same
    /// emptiness means "not read yet", which is a different thing and must not be said.
    /// </summary>
    [Reactive] public partial bool ShowNothingWaiting { get; private set; }

    // --- preview strip ---

    [Reactive] public partial string? PreviewingPath { get; private set; }
    [Reactive] public partial string PreviewingName { get; private set; }
    [Reactive] public partial TimeSpan PreviewPosition { get; set; }
    [Reactive] public partial TimeSpan PreviewDuration { get; private set; }

    /// <summary>Seconds, because a progress bar works in numbers rather than in TimeSpans.</summary>
    [ObservableAsProperty] public partial double PreviewPositionSeconds { get; }
    [ObservableAsProperty] public partial double PreviewDurationSeconds { get; }

    [ObservableAsProperty] public partial bool IsPreviewing { get; }

    public TaggingViewModel(
        ILibraryIndex libraryIndex,
        ITrackStore trackStore,
        IDanceListStore danceListStore,
        IPreviewPlaybackService preview,
        INotificationService notifications,
        ILoggerService loggerService)
    {
        _libraryIndex = libraryIndex;
        _danceListStore = danceListStore;
        _preview = preview;
        _notifications = notifications;
        _loggerService = loggerService;

        Suggestions = [];
        Unrecognised = [];
        OneOffs = [];
        AllDances = [];
        SummaryText = string.Empty;
        ScanProgressText = string.Empty;
        PreviewingName = string.Empty;

        _previewPositionSecondsHelper = this.WhenAnyValue(x => x.PreviewPosition)
            .Select(position => position.TotalSeconds)
            .ToProperty(this, x => x.PreviewPositionSeconds);
        _previewPositionSecondsHelper.DisposeWith(_disposables);

        _previewDurationSecondsHelper = this.WhenAnyValue(x => x.PreviewDuration)
            .Select(duration => duration.TotalSeconds)
            .ToProperty(this, x => x.PreviewDurationSeconds);
        _previewDurationSecondsHelper.DisposeWith(_disposables);

        _isPreviewingHelper = this.WhenAnyValue(x => x.PreviewingPath)
            .Select(path => path is not null)
            .ToProperty(this, x => x.IsPreviewing);
        _isPreviewingHelper.DisposeWith(_disposables);

        preview.WhenPreviewChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(OnPreviewChanged)
            .DisposeWith(_disposables);

        preview.WhenProgressChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(position => PreviewPosition = position)
            .DisposeWith(_disposables);

        preview.WhenDurationChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(duration => PreviewDuration = duration)
            .DisposeWith(_disposables);

        // The report waits for the scan and then rebuilds itself. A first run reaches this step
        // while thousands of files are still being read, and a report of what has been read so far
        // is worse than no report at all.
        // The list is built once, when the scan is over. Rebuilding it while files are still
        // arriving makes every row jump under the pointer, and a row that disappears mid-click
        // takes the click with it: unusable, and for no benefit, since nothing can be answered
        // until the library has been read anyway.
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

        // While it runs, the only thing that moves is a count. Nothing here can be clicked.
        this.WhenAnyValue(x => x.IsScanning)
            .Select(scanning => scanning
                ? Observable.Interval(TimeSpan.FromSeconds(1)).Select(_ => System.Reactive.Unit.Default)
                : Observable.Empty<System.Reactive.Unit>())
            .Switch()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => ScanProgressText = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Tagging_ScanProgress, trackStore.Current.Count))
            .DisposeWith(_disposables);

        // The list is deliberately not rebuilt when the dance list changes. Answering a value adds
        // a spelling to the dance list, and rebuilding on that would delete the row the user just
        // answered, from under their pointer, every single time.
    }

    /// <summary>Rebuilds the report from the index.</summary>
    [ReactiveCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var entries = await _libraryIndex.SnapshotByPathAsync();
            var ignored = await _libraryIndex.GetIgnoredValuesAsync();
            var report = ScanReportBuilder.Build([.. entries.Values], _danceListStore.Index, ignored);

            Suggestions =
            [
                .. report.Unrecognised
                    .Where(value => value.Kind == UnrecognisedKind.Misspelled && value.TrackCount > 1)
                    .Select(value => new UnrecognisedValueViewModel(value))
            ];
            Unrecognised =
            [
                .. report.Unrecognised
                    .Where(value => value.Kind != UnrecognisedKind.Misspelled && value.TrackCount > 1)
                    .Select(value => new UnrecognisedValueViewModel(value))
            ];
            OneOffs =
            [
                .. report.Unrecognised
                    .Where(value => value.TrackCount == 1)
                    .Select(value => new UnrecognisedValueViewModel(value))
            ];

            AllDances =
            [
                .. _danceListStore.Current.Dances
                    .Select(dance => new DanceSuggestionRow(
                        new DanceSuggestion(dance.Slug, dance.DisplayName, 0)))
                    .OrderBy(row => row.Label, StringComparer.CurrentCulture)
            ];

            HasAnythingToReport = report.HasAnythingToReport;
            ShowNothingWaiting = !report.HasAnythingToReport && !IsScanning;
            SummaryText = string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.Tagging_Summary,
                report.Complete,
                report.UnrecognisedTrackCount,
                report.Unrecognised.Count,
                report.SilentlyUnresolved);
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to build the tagging report", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Points every track claiming a value at a dance.</summary>
    /// <remarks>
    /// The answer is kept against the tracks rather than taught to the dance list: the list is
    /// BigBalfolkList's, and a spelling worth having belongs in a proposal there rather than in one
    /// person's copy. Files already answered stay answered; a new file spelled the same way asks
    /// again.
    /// </remarks>
    public async Task MapAsync(UnrecognisedValueViewModel value, DanceSuggestionRow suggestion)
    {
        if (!value.CanMapAsAWhole)
        {
            return;
        }

        if (await AssignAsync(value.Value.Paths, suggestion.Suggestion.Slug))
        {
            value.DecidedAs = suggestion.Suggestion.DisplayName;
            value.IsIgnored = false;
            foreach (var track in value.Tracks)
            {
                track.DecidedAs = suggestion.Suggestion.DisplayName;
            }
        }
    }

    /// <summary>Points one folder's worth of tracks at a dance, for a value too general to map whole.</summary>
    public async Task MapFolderAsync(FolderGroupViewModel folder, DanceSuggestionRow suggestion)
    {
        if (await AssignAsync(folder.Breakdown.Paths, suggestion.Suggestion.Slug))
        {
            folder.DecidedAs = suggestion.Suggestion.DisplayName;
            foreach (var track in folder.Tracks)
            {
                track.DecidedAs = suggestion.Suggestion.DisplayName;
            }
        }
    }

    /// <summary>Points a single track at a dance.</summary>
    public async Task MapTrackAsync(PreviewRowViewModel track, DanceSuggestionRow suggestion)
    {
        if (await AssignAsync([track.Path], suggestion.Suggestion.Slug))
        {
            track.DecidedAs = suggestion.Suggestion.DisplayName;
        }
    }

    /// <summary>
    /// Says this value is not a dance and stops it being asked about.
    /// </summary>
    /// <remarks>
    /// A first-class answer, not a way of putting something off. A real library is full of genres
    /// and band names, and without this the badge sits at 137 forever.
    /// </remarks>
    /// <summary>Marks a value as not a dance, or takes that back.</summary>
    public async Task ToggleIgnoreAsync(UnrecognisedValueViewModel value)
    {
        if (value.IsIgnored)
        {
            await _libraryIndex.StopIgnoringValueAsync(value.Text);
            value.IsIgnored = false;
            return;
        }

        await _libraryIndex.IgnoreValueAsync(value.Text);
        value.IsIgnored = true;
        value.DecidedAs = null;
    }

    public async Task TogglePreviewAsync(PreviewRowViewModel track)
    {
        if (string.Equals(_preview.Previewing, track.Path, StringComparison.Ordinal))
        {
            await _preview.StopAsync();
            return;
        }

        if (!await _preview.PlayAsync(track.Path))
        {
            // The room is listening to something else, and this is preparation work.
            _notifications.Show(UiStrings.Tagging_PreviewRefused, NotificationSeverity.Warning);
        }
    }

    [ReactiveCommand]
    private void StopPreview() => _preview.StopAsync().SafeFireAndForget(
        exception => _loggerService.ErrorAsync("Failed to stop the preview", exception));

    public Task SeekPreviewAsync(TimeSpan position) => _preview.SeekAsync(position);

    public void Dispose()
    {
        _preview.StopAsync().SafeFireAndForget(exception =>
            _loggerService.ErrorAsync("Failed to stop the preview", exception));
        _disposables.Dispose();
    }

    /// <summary>
    /// Records a decision. Deliberately does not rebuild the list: the answered row stays where it
    /// is, marked, so it can be seen and corrected.
    /// </summary>
    private async Task<bool> AssignAsync(IReadOnlyList<string> paths, string slug)
    {
        try
        {
            await _libraryIndex.AssignDanceAsync(paths, slug);
            return true;
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to assign a dance", exception);
            return false;
        }
    }

    private void OnPreviewChanged(string? path)
    {
        foreach (var row in AllRows())
        {
            row.IsPreviewing = string.Equals(row.Path, path, StringComparison.Ordinal);
        }

        PreviewingPath = path;
        PreviewingName = path is null ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
        if (path is null)
        {
            PreviewPosition = TimeSpan.Zero;
        }
    }

    private IEnumerable<PreviewRowViewModel> AllRows() =>
        Suggestions.Concat(Unrecognised).Concat(OneOffs)
            .SelectMany(value => value.Tracks.Concat(value.Folders.SelectMany(folder => folder.Tracks)));
}
