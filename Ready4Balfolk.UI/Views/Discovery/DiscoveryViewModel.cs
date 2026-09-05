using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.UI.Resources;

namespace Ready4Balfolk.UI.Views.Discovery;

/// <summary>Where the user tells the application what shape their library is.</summary>
/// <remarks>
/// <para>
/// Every control here is a bulk approval. Code can measure that strings agree; only a person can say
/// that a rule is right, and once they have, the code stops hedging and powers through the files the
/// rule covers. That is the only way a library of thousands is answered in an evening rather than
/// never.
/// </para>
/// <para>
/// The price of that bargain is that the greenlight has to be informed, so nothing here can be
/// agreed to without its blast radius on screen first: how many files a rule takes, what it makes of
/// them, and what would be left over.
/// </para>
/// </remarks>
#pragma warning disable CS8618 // ObservableAsProperty fields are set by the helpers in the constructor
public sealed partial class DiscoveryViewModel : ReactiveObject, IDisposable
{
    /// <summary>Deep enough for any real library, and a level nobody has is not worth a row.</summary>
    private const int MaximumFolderLevels = 6;

    private readonly ISettingsStore _settingsStore;
    private readonly ILibraryIndex _libraryIndex;
    private readonly IDanceListStore _danceListStore;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];

    private IReadOnlyList<string> _fileNames = [];
    private IReadOnlyList<IReadOnlyList<string>> _folders = [];
    private IReadOnlyList<CalibrationFile> _library = [];
    private Dictionary<string, int> _customTagCounts = new(StringComparer.OrdinalIgnoreCase);
    private int _indexedFiles;

    /// <summary>Whether the controls a person answers have been filled in from disk yet.</summary>
    /// <remarks>
    /// Reading a library of thousands takes minutes, and this screen refreshes when it finishes.
    /// Everything measured is measured again then, which is the point, but what the person has been
    /// doing while they waited is not the settings file's to overwrite: a tick made during the scan
    /// vanishing the moment it completes is the worst possible moment to lose it.
    /// </remarks>
    private bool _shownWhatIsOnDisk;

    public DiscoveryViewModel(
        ISettingsStore settingsStore,
        ILibraryIndex libraryIndex,
        IDanceListStore danceListStore,
        ITrackStore trackStore,
        ILoggerService loggerService)
    {
        _settingsStore = settingsStore;
        _libraryIndex = libraryIndex;
        _danceListStore = danceListStore;
        _loggerService = loggerService;

        DraftPattern = string.Empty;
        CustomDanceTag = string.Empty;
        CustomDanceTagSummary = string.Empty;

        // Live, like the pattern preview: naming a tag is a declaration and a bulk approval, and
        // the greenlight has to be informed. This is the blast radius of the custom dance tag.
        this.WhenAnyValue(x => x.CustomDanceTag)
            .Subscribe(_ => SummariseCustomTag())
            .DisposeWith(_disposables);
        DraftSummary = string.Empty;
        CoverageSummary = string.Empty;
        DraftSamples = [];
        DraftMisses = [];
        ScanProgressText = string.Empty;

        // While the scan runs the only thing that moves is a count, read from the index rather than
        // from the library: nothing reaches the library during a scan, so counting that would sit
        // at nil. It is the whole of this screen until the reading is done.
        this.WhenAnyValue(x => x.IsScanning)
            .Select(scanning => scanning
                ? Observable.Interval(TimeSpan.FromSeconds(1))
                : Observable.Empty<long>())
            .Switch()
            .SelectMany(_ => Observable.FromAsync(() => _libraryIndex.CountIndexedAsync()))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(count => ScanProgressText = string.Format(
                CultureInfo.CurrentCulture, UiStrings.Discovery_ScanningCount, count))
            .DisposeWith(_disposables);

        // A rule is measured against the library, so measuring one while the library is still being
        // read would put a number in front of the user that is not the number they are agreeing to.
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

        TagFields =
        [
            new TagTrustFieldViewModel(TrackField.Dance, UiStrings.Discovery_FieldDance, null),
            new TagTrustFieldViewModel(TrackField.Artist, UiStrings.Discovery_FieldArtist, null),
            new TagTrustFieldViewModel(TrackField.Title, UiStrings.Discovery_FieldTitle, null)
        ];

        // The preview runs as the pattern is typed, because a rule is agreed to on the strength of
        // what it does, and asking for a separate "check" step is asking for it to be skipped.
        this.WhenAnyValue(x => x.DraftPattern)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => PreviewDraft())
            .DisposeWith(_disposables);
    }

    [Reactive] public partial bool IsBusy { get; private set; }

    /// <summary>
    /// Whether each way of reading the library is switched on. All four start off.
    /// </summary>
    /// <remarks>
    /// Most libraries are read by one of these, and four sections of settings for three mechanisms
    /// that do not apply is the most overwhelming part of the setup. Off is also off rather than
    /// merely folded away: what a section holds is kept, and does nothing until it is ticked.
    /// </remarks>
    [Reactive] public partial bool UsesFileNamePatterns { get; set; }

    [Reactive] public partial bool UsesFolderRoles { get; set; }

    [Reactive] public partial bool UsesTagTrust { get; set; }

    [Reactive] public partial bool UsesCustomDanceTag { get; set; }

    /// <summary>True while the library is being read, so no rule can be measured yet.</summary>
    [Reactive] public partial bool IsScanning { get; private set; }

    [Reactive] public partial string ScanProgressText { get; private set; }

    /// <summary>The pattern being written, not yet declared and not yet doing anything.</summary>
    [Reactive] public partial string DraftPattern { get; set; }

    /// <summary>What it would do, in the numbers a person needs before saying yes.</summary>
    [Reactive] public partial string DraftSummary { get; private set; }

    [Reactive] public partial bool CanDeclareDraft { get; private set; }

    /// <summary>What it would make of the files it takes.</summary>
    [Reactive] public partial IReadOnlyList<PatternSampleRow> DraftSamples { get; private set; }

    /// <summary>What it would leave for a person, which is the other half of the price.</summary>
    [Reactive] public partial IReadOnlyList<string> DraftMisses { get; private set; }

    [Reactive] public partial bool HasDraftSamples { get; private set; }

    [Reactive] public partial bool HasDraftMisses { get; private set; }

    /// <summary>How much of the library the declared rules account for between them.</summary>
    [Reactive] public partial string CoverageSummary { get; private set; }

    public ObservableCollection<DeclaredPatternViewModel> Patterns { get; } = [];

    public ObservableCollection<FolderLevelViewModel> Levels { get; } = [];

    public IReadOnlyList<TagTrustFieldViewModel> TagFields { get; }

    /// <summary>The name of the custom tag declared to hold the dance, empty for none.</summary>
    [Reactive] public partial string CustomDanceTag { get; set; }

    /// <summary>How many files carry a tag of that name, so the declaration is given knowingly.</summary>
    [Reactive] public partial string CustomDanceTagSummary { get; private set; }

    /// <summary>
    /// What the library's own strings suggest, measured over what no rule covers yet.
    /// </summary>
    /// <remarks>
    /// Aimed at the leftovers rather than at everything, because that is the pile a next rule is
    /// for: declare one, it swallows two thousand, and the honest question is what the six hundred
    /// still waiting look like.
    /// </remarks>
    public ObservableCollection<ProposalViewModel> Proposals { get; } = [];

    [Reactive] public partial bool HasProposals { get; private set; }

    /// <summary>Reads the library and the settings, and shows what the current rules are doing.</summary>
    [ReactiveCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _libraryIndex.OpenAsync();
            // Reachable rows only. A rule is measured so that somebody can vouch for it, and a
            // measurement over files that are not there is a number about a library nobody has.
            var snapshot = (await _libraryIndex.SnapshotByPathAsync())
                .Where(pair => pair.Value.IsAvailable)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            var root = _settingsStore.Current.MusicDirectoryPath;

            _fileNames = [.. snapshot.Keys.Select(Path.GetFileName).OfType<string>()];
            _indexedFiles = snapshot.Count;
            _customTagCounts = snapshot.Values
                .SelectMany(entry => entry.CustomTagNames.Distinct(StringComparer.OrdinalIgnoreCase))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            _folders = [.. snapshot.Keys.Select(path => SegmentsBetween(path, root))];
            _library =
            [
                .. snapshot.Values.Select(entry => new CalibrationFile
                {
                    FileName = Path.GetFileNameWithoutExtension(entry.Path),
                    Folders = SegmentsBetween(entry.Path, root),
                    // Only what a tag actually said. A value a rule or a folder answered would have
                    // calibration confirming its own settings back to itself.
                    TagArtist = entry.ArtistFrom.Kind is ClaimSourceKind.Tag ? entry.Artist : null,
                    TagTitle = entry.TitleFrom.Kind is ClaimSourceKind.Tag ? entry.Title : null
                })
            ];

            Apply(_settingsStore.Current.Discovery);
        }
        catch (Exception exception)
        {
            await _loggerService.ErrorAsync("Failed to read the library for the discovery screen", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Adds the draft to the rules, which approves every file it matches in one act.</summary>
    [ReactiveCommand]
    private async Task DeclareDraftAsync()
    {
        if (!CanDeclareDraft)
        {
            return;
        }

        var patterns = Current().FileNamePatterns.Append(DraftPattern.Trim()).ToList();
        await SaveAsync(Edited() with { FileNamePatterns = patterns });

        DraftPattern = string.Empty;
    }

    /// <summary>
    /// Takes a rule back out, which takes back the approval it gave.
    /// </summary>
    /// <remarks>
    /// The user vouched for the rule rather than for each file it touched, so removing it puts those
    /// files back in review. What they answered one at a time is not affected.
    /// </remarks>
    [ReactiveCommand]
    private async Task RemovePatternAsync(DeclaredPatternViewModel row) =>
        await SaveAsync(Edited() with
        {
            FileNamePatterns = [.. Current().FileNamePatterns.Where(text => !string.Equals(text, row.Text, StringComparison.Ordinal))]
        });

    /// <summary>Moves a rule earlier. Order is how the user says which of two shapes they mean.</summary>
    [ReactiveCommand]
    private async Task MovePatternUpAsync(DeclaredPatternViewModel row) => await MoveAsync(row, -1);

    [ReactiveCommand]
    private async Task MovePatternDownAsync(DeclaredPatternViewModel row) => await MoveAsync(row, 1);

    /// <summary>Saves the folder roles and the tag trust as they stand on screen.</summary>
    [ReactiveCommand]
    private async Task ApplyRolesAndTagsAsync() =>
        await SaveAsync(Edited() with
        {
            FolderRoles = [.. Levels.Select(level => level.Role)],
            TagTrust = new TagTrust
            {
                Dance = TagFields[0].Declared,
                Artist = TagFields[1].Declared,
                Title = TagFields[2].Declared
            },
            CustomDanceTag = string.IsNullOrWhiteSpace(CustomDanceTag) ? null : CustomDanceTag.Trim()
        });

    public void Dispose() => _disposables.Dispose();

    private DiscoverySettings Current() => _settingsStore.Current.Discovery;

    /// <summary>The stored settings with the switches as they stand on screen.</summary>
    /// <remarks>
    /// Everything that saves goes through here, because a pattern declared while the section it
    /// belongs to was ticked a moment ago must not be stored under a switch that is still off.
    /// </remarks>
    private DiscoverySettings Edited() => Current() with
    {
        UsesFileNamePatterns = UsesFileNamePatterns,
        UsesFolderRoles = UsesFolderRoles,
        UsesTagTrust = UsesTagTrust,
        UsesCustomDanceTag = UsesCustomDanceTag
    };

    private async Task MoveAsync(DeclaredPatternViewModel row, int offset)
    {
        var patterns = Current().FileNamePatterns.ToList();
        var at = patterns.FindIndex(text => string.Equals(text, row.Text, StringComparison.Ordinal));
        var to = at + offset;

        if (at < 0 || to < 0 || to >= patterns.Count)
        {
            return;
        }

        (patterns[at], patterns[to]) = (patterns[to], patterns[at]);
        await SaveAsync(Edited() with { FileNamePatterns = patterns });
    }

    private async Task SaveAsync(DiscoverySettings settings)
    {
        await _settingsStore.UpdateAsync(current => current with { DiscoveryOrNull = settings });
        Apply(settings);
    }

    /// <summary>Shows what the given rules do to the library that was read.</summary>
    /// <remarks>
    /// What is measured is redone every time. What a person answers is filled in once, and after
    /// that it is theirs: a save comes back through here carrying what they just said, so there is
    /// nothing to put back.
    /// </remarks>
    private void Apply(DiscoverySettings settings)
    {
        Patterns.Clear();
        foreach (var pattern in settings.FileNamePatterns)
        {
            Patterns.Add(new DeclaredPatternViewModel(DeclarationPreview.ForPattern(pattern, _fileNames)));
        }

        Levels.Clear();
        foreach (var level in Enumerable.Range(1, LevelsWorthShowing()))
        {
            Levels.Add(new FolderLevelViewModel(
                DeclarationPreview.ForFolderLevel(level, _folders), settings.RoleForLevel(level)));
        }

        if (!_shownWhatIsOnDisk)
        {
            for (var i = 0; i < TagFields.Count; i++)
            {
                var declared = i switch
                {
                    0 => settings.TagTrust.Dance,
                    1 => settings.TagTrust.Artist,
                    _ => settings.TagTrust.Title
                };

                TagFields[i].ShowDeclared(declared);
            }

            UsesFileNamePatterns = settings.UsesFileNamePatterns;
            UsesFolderRoles = settings.UsesFolderRoles;
            UsesTagTrust = settings.UsesTagTrust;
            UsesCustomDanceTag = settings.UsesCustomDanceTag;

            CustomDanceTag = settings.CustomDanceTag ?? string.Empty;
            _shownWhatIsOnDisk = true;
        }

        SummariseCustomTag();

        Measure(settings);

        var coverage = DeclarationPreview.ForPatterns(settings, _fileNames);
        CoverageSummary = string.Format(
            CultureInfo.CurrentCulture, UiStrings.Discovery_Coverage, coverage.Matched, coverage.Total, coverage.Missed);

        PreviewDraft();
    }

    /// <summary>
    /// Works out what the leftovers look like, and offers it.
    /// </summary>
    /// <remarks>
    /// Computed here, when the screen is opened or a rule changes, and never pushed. The application
    /// runs in front of a room, and "480 files look like %a - %t, declare it?" during a dance is
    /// unforgivable.
    /// </remarks>
    private void Measure(DiscoverySettings settings)
    {
        Proposals.Clear();

        // Leftovers carry extensions and calibration files do not; compared without them, so a
        // .flac library is measured too rather than only whatever happened to be an .mp3.
        var leftovers = DeclarationPreview.Leftovers(settings, _fileNames)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var waiting = _library
            .Where(file => leftovers.Contains(file.FileName))
            .ToList();

        var report = Calibration.Measure(
            waiting.Count > 0 ? waiting : _library, _danceListStore.Index, settings);

        foreach (var folder in report.Folders)
        {
            Proposals.Add(ProposalViewModel.From(folder));
        }

        foreach (var shape in report.Shapes)
        {
            Proposals.Add(ProposalViewModel.From(shape));
        }

        HasProposals = Proposals.Count > 0;
    }

    /// <summary>Takes a proposal, which declares it: the same act as writing the rule by hand.</summary>
    [ReactiveCommand]
    private async Task AcceptProposalAsync(ProposalViewModel? proposal)
    {
        if (proposal is null || !proposal.CanAccept)
        {
            return;
        }

        var current = Edited();

        // Taking a proposal is the same act as writing the rule by hand, so it switches its section
        // on. Storing a rule under a switch that is off would be accepting something that then sits
        // there doing nothing.
        await SaveAsync(proposal.IsFolderRole
            ? current with
            {
                UsesFolderRoles = true,
                FolderRoles = WithRole(current.FolderRoles, proposal.Level, proposal.Role)
            }
            : current with
            {
                UsesFileNamePatterns = true,
                FileNamePatterns = [.. current.FileNamePatterns, proposal.Pattern!]
            });
    }

    /// <summary>Turns a proposal down, which leaves the level or the shape as it was: unknown.</summary>
    [ReactiveCommand]
    private void DismissProposal(ProposalViewModel? proposal)
    {
        if (proposal is not null)
        {
            Proposals.Remove(proposal);
            HasProposals = Proposals.Count > 0;
        }
    }

    private static List<FolderRole> WithRole(IReadOnlyList<FolderRole> roles, int level, FolderRole role)
    {
        var updated = roles.ToList();
        while (updated.Count < level)
        {
            updated.Add(FolderRole.Unknown);
        }

        updated[level - 1] = role;
        return updated;
    }

    /// <summary>How deep the library actually goes, so no row is offered for a level nobody has.</summary>
    private int LevelsWorthShowing()
    {
        var deepest = _folders.Count == 0 ? 0 : _folders.Max(segments => segments.Count);
        return Math.Min(deepest, MaximumFolderLevels);
    }

    private void SummariseCustomTag()
    {
        var name = CustomDanceTag.Trim();
        CustomDanceTagSummary = name.Length == 0
            ? string.Empty
            : string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.Discovery_CustomDanceTagCount,
                _customTagCounts.GetValueOrDefault(name),
                _indexedFiles);
    }

    /// <summary>Measures the draft now rather than on the typing throttle.</summary>
    public void PreviewDraftNow() => PreviewDraft();

    /// <summary>
    /// Measures the draft against what no rule has taken yet.
    /// </summary>
    /// <remarks>
    /// Against the leftovers rather than the whole library, because that is the pile the next rule
    /// is aimed at: declare one, it swallows two thousand, and the honest question about the next is
    /// what it does to the six hundred that are left.
    /// </remarks>
    private void PreviewDraft()
    {
        if (string.IsNullOrWhiteSpace(DraftPattern))
        {
            DraftSummary = string.Empty;
            DraftSamples = [];
            DraftMisses = [];
            HasDraftSamples = false;
            HasDraftMisses = false;
            CanDeclareDraft = false;
            return;
        }

        var leftovers = DeclarationPreview.Leftovers(Current(), _fileNames);
        var preview = DeclarationPreview.ForPattern(DraftPattern.Trim(), leftovers);

        DraftSummary = DiscoveryText.Summarise(preview);
        DraftSamples = [.. preview.Matches.Select(PatternSampleRow.From)];
        DraftMisses = preview.Misses;
        HasDraftSamples = DraftSamples.Count > 0;
        HasDraftMisses = DraftMisses.Count > 0;
        CanDeclareDraft = preview.Problem is PatternProblem.None
            && !Current().FileNamePatterns.Contains(DraftPattern.Trim(), StringComparer.Ordinal);
    }

    /// <summary>The folders between the music directory and a file, outermost first.</summary>
    private static IReadOnlyList<string> SegmentsBetween(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root) || Path.GetDirectoryName(path) is not { } directory)
        {
            return [];
        }

        var relative = Path.GetRelativePath(root, directory);

        return relative is "." || relative.StartsWith("..", StringComparison.Ordinal)
            ? []
            : [.. relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)];
    }
}
