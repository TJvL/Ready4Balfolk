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
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.Discovery;

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
/// for. It is keyboard driven for the same reason: 2668 mouse trips is the difference between an
/// evening and never.
/// </para>
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class ReviewViewModel : ReactiveObject, IDisposable
{
    /// <summary>A folder bigger than this is confirmed before it is answered.</summary>
    private const int AsksFirstAbove = 25;


    private readonly ILibraryIndex _libraryIndex;
    private readonly IDanceListStore _danceListStore;
    private readonly ISettingsStore _settingsStore;
    private readonly ITrackStore _trackStore;
    private readonly IPreviewPlaybackService _preview;
    private readonly IConfirmationService _confirmations;
    private readonly INotificationService _notifications;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    // Dropped and rebuilt with the rows: one subscription per row, watching what is typed into it.
    private readonly CompositeDisposable _rowSubscriptions = [];

    public ReviewViewModel(
        ILibraryIndex libraryIndex,
        IDanceListStore danceListStore,
        ISettingsStore settingsStore,
        ITrackStore trackStore,
        IPreviewPlaybackService preview,
        INotificationService notifications,
        IConfirmationService confirmations,
        DiscoveryViewModel discovery,
        NavigationService navigation,
        ILoggerService loggerService)
    {
        Discovery = discovery;
        _libraryIndex = libraryIndex;
        _danceListStore = danceListStore;
        _settingsStore = settingsStore;
        _trackStore = trackStore;
        _preview = preview;
        _confirmations = confirmations;
        _notifications = notifications;
        _loggerService = loggerService;

        Summary = string.Empty;
        ScanProgressText = string.Empty;
        AllDances = [];

        _previewPositionSecondsHelper = this.WhenAnyValue(x => x.PreviewPosition)
            .Select(position => position.TotalSeconds)
            .ToProperty(this, x => x.PreviewPositionSeconds);
        _previewPositionSecondsHelper.DisposeWith(_disposables);

        _previewDurationSecondsHelper = this.WhenAnyValue(x => x.PreviewDuration)
            .Select(duration => duration.TotalSeconds)
            .ToProperty(this, x => x.PreviewDurationSeconds);
        _previewDurationSecondsHelper.DisposeWith(_disposables);

        preview.WhenPreviewChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(OnPreviewChanged)
            .DisposeWith(_disposables);

        // The screen owns its preview. Navigating away must not leave the room hearing a file
        // nobody is looking at; the wizard's inner steps are handled by the wizard, since no
        // screen change happens between them.
        navigation.WhenAnyValue(x => x.CurrentScreen)
            .Skip(1)
            .Where(screen => screen is not Screen.Review)
            .Subscribe(_ => StopPreviewAsync().SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to stop the preview on leaving the screen", exception)))
            .DisposeWith(_disposables);

        preview.WhenProgressChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(position => PreviewPosition = position)
            .DisposeWith(_disposables);

        preview.WhenDurationChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(duration => PreviewDuration = duration)
            .DisposeWith(_disposables);

        AllowDancesOutsideTheList = settingsStore.Current.AllowDancesOutsideTheList;

        // Skip(1) so restoring the stored value above is not written straight back, and the queue is
        // rebuilt afterwards because the rule decides what is still in it.
        this.WhenAnyValue(x => x.AllowDancesOutsideTheList)
            .Skip(1)
            .DistinctUntilChanged()
            .SelectMany(allow => Observable.FromAsync(async () =>
            {
                await settingsStore.UpdateAsync(current => current with { AllowDancesOutsideTheList = allow });
                await RefreshCommand.Execute().FirstAsync();
            }))
            .Subscribe(_ => { }, exception =>
                _loggerService.ErrorAsync("Failed to change the dance rule", exception).SafeFireAndForget())
            .DisposeWith(_disposables);

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
            .SelectMany(_ => Observable.FromAsync(() => _libraryIndex.CountIndexedAsync()))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(count => ScanProgressText = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Review_Scanning, count))
            .DisposeWith(_disposables);
    }

    /// <summary>True while the library is being read, which on a first run is most of this screen.</summary>
    [Reactive] public partial bool IsScanning { get; private set; }

    /// <summary>How far the scan has got. The only thing that moves while it runs.</summary>
    [Reactive] public partial string ScanProgressText { get; private set; }

    /// <summary>
    /// The rules, on the screen where their effect is visible.
    /// </summary>
    /// <remarks>
    /// A rule exists to empty this queue, so it belongs above it rather than three screens away in
    /// the settings: declare one, watch the queue shrink. Collapsed until asked for, because most
    /// of the time the queue is what somebody came here for.
    /// </remarks>
    public DiscoveryViewModel Discovery { get; }

    [Reactive] public partial bool IsDiscoveryOpen { get; set; }

    /// <summary>
    /// Whether a dance the published list does not carry may still reach the library.
    /// </summary>
    /// <remarks>
    /// It lives here because this is where somebody meets the consequence of it being off: a row
    /// they have answered, sitting in amber, waiting on a list they do not control.
    /// </remarks>
    [Reactive] public partial bool AllowDancesOutsideTheList { get; set; }

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

    [Reactive] public partial TimeSpan PreviewPosition { get; private set; }

    [Reactive] public partial TimeSpan PreviewDuration { get; private set; }

    /// <summary>Seconds, because a progress bar cannot be bound to a TimeSpan.</summary>
    [ObservableAsProperty] public partial double PreviewPositionSeconds { get; }

    [ObservableAsProperty] public partial double PreviewDurationSeconds { get; }

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

            var ignored = await _libraryIndex.GetIgnoredValuesAsync();
            var groups = ReviewQueueBuilder.Build(
                entries,
                approvals,
                dances,
                _settingsStore.Current.MusicDirectoryPath,
                ignored,
                _settingsStore.Current.AllowDancesOutsideTheList);

            AllDances = [.. dances.Dances.Select(dance => dance.DisplayName).OrderBy(name => name, StringComparer.CurrentCulture)];

            Rows.Clear();
            _rowSubscriptions.Clear();

            foreach (var group in groups)
            {
                var first = true;
                foreach (var track in group.Tracks)
                {
                    var row = new ReviewRowViewModel(track, first, AllDances);
                    Rows.Add(row);
                    first = false;

                    // A folder becomes answerable the moment its last blank is filled, and the
                    // button that says so was being decided once, while every box was still empty.
                    // Only this row's folder: a keystroke cannot change any other folder's
                    // count, and regrouping the whole queue per keystroke was O(queue) work on
                    // exactly the screen that holds thousands of rows.
                    row.WhenAnyValue(candidate => candidate.Dance, candidate => candidate.Artist, candidate => candidate.Title)
                        .Skip(1)
                        .Subscribe(_ => LabelFolder(row.Folder))
                        .DisposeWith(_rowSubscriptions);

                    // What the list can offer for what has been typed, recomputed as it is typed.
                    row.WhenAnyValue(candidate => candidate.Dance)
                        .Skip(1)
                        .Subscribe(_ => row.ShowMatches())
                        .DisposeWith(_rowSubscriptions);
                }
            }

            LabelFolderButtons();
            // The first row nobody has dealt with. Starting on an answered or parked one makes the
            // first keystroke of a sitting land on work that is already done.
            Selected = FirstWaiting() ?? Rows.FirstOrDefault();
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
        if (row is null)
        {
            return;
        }

        if (!row.CanApprove)
        {
            // Something is missing, so there is nothing to agree to. Said by the row rather than by
            // a message, because the row is what the person is looking at.
            row.Reject();
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
    /// <para>
    /// It confirms rather than fills in: each row keeps the artist and title it already has, and a
    /// row still missing one is left alone rather than being quietly approved as blank. A folder is
    /// where the remaining evidence is, and confirming one is the difference between an evening and
    /// never.
    /// </para>
    /// <para>
    /// Above a handful it asks first. A library with everything in one directory is a single group
    /// of two thousand, and a keystroke that answers all of them without saying so is not a bulk
    /// confirm, it is an accident.
    /// </para>
    /// </remarks>
    [ReactiveCommand]
    private async Task ApproveFolderAsync(ReviewRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        // Every row in the folder that is not complete says so at once, which is the fastest way to
        // see what a folder is still missing and where. It happens whether or not anything is left
        // to answer: asking a second time has to point at the same rows as the first, not at
        // whichever one the keys happen to be on.
        var incomplete = Incomplete(row).ToList();
        foreach (var candidate in incomplete)
        {
            candidate.Reject();
        }

        var answerable = Answerable(row).ToList();
        if (answerable.Count == 0)
        {
            // Nothing to take and nothing holding it up either: the row itself is the answer.
            if (incomplete.Count == 0)
            {
                row.Reject();
            }

            return;
        }

        if (answerable.Count > AsksFirstAbove
            && !await _confirmations.ConfirmAsync(
                UiStrings.Review_ApproveFolderConfirmTitle,
                string.Format(
                    CultureInfo.CurrentCulture,
                    UiStrings.Review_ApproveFolderConfirm,
                    answerable.Count,
                    row.FolderText),
                UiStrings.Review_ApproveFolderYes,
                UiStrings.Review_ApproveFolderNo))
        {
            return;
        }

        foreach (var sibling in answerable)
        {
            await ApproveRowAsync(sibling);
        }

        await _trackStore.RefreshLibraryAsync();

        LabelFolderButtons();
        Selected = NextAfter(row);
    }

    /// <summary>The rows of a folder that are holding it up: waiting, and missing something.</summary>
    private IEnumerable<ReviewRowViewModel> Incomplete(ReviewRowViewModel row) =>
        row.IsInFolder
            ? Rows.Where(candidate =>
                string.Equals(candidate.Folder, row.Folder, StringComparison.Ordinal)
                && !candidate.IsApproved
                && !candidate.CanApprove)
            : [];

    /// <summary>The first row still waiting: not answered, and not parked on an unpublished dance.</summary>
    private ReviewRowViewModel? FirstWaiting() =>
        Rows.FirstOrDefault(row => row.State is ReviewRowState.Waiting);

    /// <summary>
    /// The rows a folder answer would take: waiting, complete as they stand, and in a folder.
    /// </summary>
    /// <remarks>
    /// Nothing for a track lying loose in the music directory. Those were filed nowhere, so there is
    /// no "these belong together" to act on, and offering one would make the button mean "answer
    /// everything I never sorted".
    /// </remarks>
    private IEnumerable<ReviewRowViewModel> Answerable(ReviewRowViewModel row) =>
        row.IsInFolder
            ? Rows.Where(candidate =>
                string.Equals(candidate.Folder, row.Folder, StringComparison.Ordinal)
                && !candidate.IsApproved
                && candidate.CanApprove)
            : [];

    /// <summary>Says on every folder button how many rows it would answer.</summary>
    private void LabelFolderButtons()
    {
        foreach (var group in Rows.GroupBy(row => row.Folder, StringComparer.Ordinal))
        {
            Label([.. group]);
        }
    }

    private void LabelFolder(string folder) =>
        Label([.. Rows.Where(row => string.Equals(row.Folder, folder, StringComparison.Ordinal))]);

    private static void Label(IReadOnlyList<ReviewRowViewModel> group)
    {
        var answerable = group.Count(row => !row.IsApproved && row.CanApprove);

        foreach (var row in group)
        {
            row.AnswerableInFolder = answerable;
            row.CanAnswerFolder = row.IsInFolder && answerable > 1;
            row.AnswerFolderText = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Review_ApproveFolderCount, answerable);
        }
    }

    /// <summary>
    /// Plays a few seconds of a track, or stops it when it is already the one playing.
    /// </summary>
    /// <remarks>
    /// Hearing it is often the only way to answer "which dance is this", and it is refused while the
    /// queue is playing: there is one output and a room is on it, so this is preparation work by
    /// construction rather than by discipline.
    /// </remarks>
    public async Task TogglePreviewAsync(ReviewRowViewModel row)
    {
        if (string.Equals(_preview.Previewing, row.Path, StringComparison.Ordinal))
        {
            await _preview.StopAsync();
            return;
        }

        if (!await _preview.PlayAsync(row.Path))
        {
            _notifications.Show(UiStrings.Review_PreviewRefused, NotificationSeverity.Warning);
        }
    }

    public Task SeekPreviewAsync(TimeSpan position) => _preview.SeekAsync(position);

    /// <summary>True while this screen is playing something, which is what the arrows then move.</summary>
    public bool IsPreviewing => _preview.Previewing is not null;

    /// <summary>Stops whatever is playing, which is what Escape is for.</summary>
    public Task StopPreviewAsync() => _preview.StopAsync();

    /// <summary>Moves through what is playing, clamped to the track.</summary>
    public Task SeekByAsync(TimeSpan delta)
    {
        if (PreviewDuration <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var target = PreviewPosition + delta;
        var clamped = target < TimeSpan.Zero
            ? TimeSpan.Zero
            : target > PreviewDuration ? PreviewDuration : target;

        return _preview.SeekAsync(clamped);
    }

    /// <summary>Moves to the row above or below, which is what the arrows do outside a suggestion list.</summary>
    public void Step(int direction)
    {
        if (Selected is null || Rows.Count == 0)
        {
            return;
        }

        var at = Rows.IndexOf(Selected) + direction;
        if (at >= 0 && at < Rows.Count)
        {
            Selected = Rows[at];
        }
    }

    /// <summary>One row at a time carries the playing state, so the strip lives on the row itself.</summary>
    private void OnPreviewChanged(string? path)
    {
        foreach (var row in Rows)
        {
            row.IsPreviewing = string.Equals(row.Path, path, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Gives every waiting track claiming the same thing the dance this row was given.
    /// </summary>
    /// <remarks>
    /// The dance and nothing else: an artist and a title are per track, so those thirty-four files
    /// still want their own confirmation. What this kills is answering one misspelling thirty-four
    /// times, which is the difference between an evening and never.
    /// </remarks>
    [ReactiveCommand]
    private async Task UseDanceForAllAsync(ReviewRowViewModel? row)
    {
        if (row is null || !row.IsShared || string.IsNullOrWhiteSpace(row.Dance))
        {
            return;
        }

        var dance = row.Dance.Trim();
        var sharing = Sharing(row).ToList();

        await _libraryIndex.ApproveIndividuallyAsync(
            [.. sharing.Select(candidate => candidate.Path)], [new FieldAnswer(TrackField.Dance, dance)]);

        foreach (var candidate in sharing)
        {
            candidate.Dance = dance;
        }

        _notifications.Show(
            string.Format(CultureInfo.CurrentCulture, UiStrings.Review_UsedForAll, sharing.Count, dance),
            NotificationSeverity.Information);
    }

    /// <summary>
    /// Says a value is junk rather than an answer, everywhere it appears.
    /// </summary>
    /// <remarks>
    /// "trad" is not a dance and never will be, and it maps to nothing: the tracks claiming it still
    /// need a real answer. Clearing it is what stops a wrong answer sitting where a person is
    /// looking for a missing one, and it is remembered, so a rescan does not put it back.
    /// </remarks>
    [ReactiveCommand]
    private async Task NotADanceAsync(ReviewRowViewModel? row)
    {
        if (row is null || !row.HasUnknownValue)
        {
            return;
        }

        await _libraryIndex.IgnoreValueAsync(row.UnknownValue);

        foreach (var candidate in Sharing(row))
        {
            candidate.ForgetUnknownValue();
        }
    }

    /// <summary>Every waiting row claiming the same unknown value, this one included.</summary>
    /// <remarks>
    /// A value that folds to nothing shares with nothing: "???" folds to the empty string, and so
    /// does a row with no value at all, so matching on the empty key would hand one decision to
    /// every unanswered row in the queue.
    /// </remarks>
    private IEnumerable<ReviewRowViewModel> Sharing(ReviewRowViewModel row)
    {
        var key = StringNormalizer.Normalize(row.UnknownValue);

        return key.Length == 0
            ? [row]
            : Rows.Where(candidate =>
                !candidate.IsApproved
                && string.Equals(StringNormalizer.Normalize(candidate.UnknownValue), key, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _rowSubscriptions.Dispose();
        _preview.StopAsync().SafeFireAndForget(
            exception => _loggerService.ErrorAsync("Failed to stop the preview", exception));
        _disposables.Dispose();
    }

    private async Task ApproveRowAsync(ReviewRowViewModel row)
    {
        await _libraryIndex.ApproveIndividuallyAsync([row.Path],
        [
            new FieldAnswer(TrackField.Dance, row.Dance.Trim()),
            new FieldAnswer(TrackField.Artist, row.Artist.Trim()),
            new FieldAnswer(TrackField.Title, row.Title.Trim())
        ]);

        // A dance the published list has never heard of is not a local problem to patch around. The
        // answer is kept, the track parks, and a proposal at BigBalfolkList is what releases it.
        var known = _danceListStore.Index.ResolveSlug(row.Dance.Trim()) is not null;
        row.MarkApproved(known);
    }

    /// <summary>
    /// The next row still waiting, so answering one lands on the next question.
    /// </summary>
    /// <remarks>
    /// Past anything answered and anything parked: a row waiting on a list nobody here controls is
    /// not a question, and stopping on it every time would make the queue read as though it were
    /// not moving.
    /// </remarks>
    private ReviewRowViewModel? NextAfter(ReviewRowViewModel row)
    {
        var at = Rows.IndexOf(row);

        return Rows.Skip(at + 1).FirstOrDefault(candidate => candidate.State is ReviewRowState.Waiting)
            ?? FirstWaiting();
    }
}
