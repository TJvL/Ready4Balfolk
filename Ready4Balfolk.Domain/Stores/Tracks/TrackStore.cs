using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using DynamicData;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Library;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public sealed class TrackStore : ITrackStore, IDisposable
{
    private const int MaxAmountOfFileReaderThreads = 32;
    private readonly ILoggerService _loggerService;
    private readonly ITrackDiscoveryService _discoveryService;
    private readonly IDanceListStore _danceListStore;
    private readonly ILibraryIndex _libraryIndex;
    private readonly IMissingFolderPrompt _missingFolderPrompt;
    private readonly SourceList<Track> _tracks = new();
    private readonly BehaviorSubject<bool> _isLoading = new(false);
    private readonly BehaviorSubject<int> _inReviewCount = new(0);
    private readonly BehaviorSubject<int> _unavailableCount = new(0);
    private readonly Subject<string> _fileVanished = new();
    private readonly IDisposable _danceListSubscription;
    private readonly LibraryWatcher _watcher;
    private readonly IDisposable _watcherSubscription;
    // Loads are started fire-and-forget from the setter, so without a gate two of them interleave:
    // each opens by disposing the watcher and clearing the track list, so one load ends up
    // disposing the watcher the other just published, and appending its tracks after the other
    // cleared them.
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCts;
    // The root the watcher's files are under, so a file it notices can still be placed relative to
    // it.
    private IDirectoryInfo? _musicRoot;
    // Compiled once and swapped whole, so a scan running over it never sees half a rule change.
    private DeclaredDiscovery _declared = DeclaredDiscovery.Undeclared;
    private TrackLibraryConfiguration _configuration = TrackLibraryConfiguration.Undeclared;
    private bool _allowDancesOutsideTheList;
    private bool _disposed;
    private readonly IFileSystem _fileSystem;

    public TrackStore(
        ILoggerService loggerService,
        ITrackDiscoveryService discoveryService,
        IDanceListStore danceListStore,
        ILibraryIndex libraryIndex,
        IFileSystem fileSystem,
        IMissingFolderPrompt missingFolderPrompt)
    {
        _loggerService = loggerService;
        _discoveryService = discoveryService;
        _danceListStore = danceListStore;
        _libraryIndex = libraryIndex;
        _fileSystem = fileSystem;
        _missingFolderPrompt = missingFolderPrompt;
        _watcher = new LibraryWatcher(fileSystem);
        _watcherSubscription = _watcher.Changes.Subscribe(OnFileChanged);

        // Skip(1): the store replays its current list to a new subscriber, and rebuilding an empty
        // library at construction is work with nothing to do.
        //
        // A rebuild rather than a re-resolve, and it is what makes a merged proposal at
        // BigBalfolkList visibly clear part of the backlog. A track parked on a value the list did
        // not carry is not in the published library at all, so there is nothing in hand to
        // re-point; the gate resolves what was approved against the list every time, so importing a
        // list that now carries the name lets those tracks through and nobody is asked again.
        _danceListSubscription = danceListStore.Observe()
            .Skip(1)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(_ => RefreshLibraryAsync().SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to rebuild the library after a dance list update", exception)));
    }

    ~TrackStore()
    {
        Dispose(false);
    }

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public IReadOnlyList<Track> Current => _tracks.Items.ToList();

    public IObservable<int> InReviewCount => _inReviewCount.AsObservable();

    public IObservable<int> UnavailableCount => _unavailableCount.AsObservable();

    public IObservable<string> WhenTrackFileVanished => _fileVanished.AsObservable();

    /// <summary>Brings the library into line with what the settings now say.</summary>
    /// <remarks>
    /// <para>
    /// One entry point rather than three setters, so the three cannot be applied in an order that
    /// makes the store do the work twice. Which of them changed decides how much work happens: a new
    /// directory is a full scan, new rules are a re-read of the same directory, and the dance rule
    /// alone is a rebuild from the index that opens no files.
    /// </para>
    /// <para>
    /// Cancels whatever is in flight before starting. Superseded sources are deliberately not
    /// disposed: the run that owns one may still be observing its token, and a
    /// CancellationTokenSource with no registered timers holds nothing worth reclaiming.
    /// </para>
    /// </remarks>
    public async Task ApplyAsync(TrackLibraryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directoryChanged = !string.Equals(
            _configuration.MusicDirectoryPath, configuration.MusicDirectoryPath, StringComparison.Ordinal);
        var rulesChanged = _configuration.Discovery != configuration.Discovery;
        var danceRuleChanged =
            _configuration.AllowDancesOutsideTheList != configuration.AllowDancesOutsideTheList;

        if (!directoryChanged && !rulesChanged && !danceRuleChanged)
        {
            return;
        }

        _configuration = configuration;
        _declared = DeclaredDiscovery.Compile(configuration.Discovery);
        _allowDancesOutsideTheList = configuration.AllowDancesOutsideTheList;

        var cancellation = new CancellationTokenSource();
        Interlocked.Exchange(ref _loadCts, cancellation)?.Cancel();
        var token = cancellation.Token;

        if (configuration.MusicDirectoryPath is not { Length: > 0 } path)
        {
            _ = _loggerService.DebugAsync("No music directory set; nothing to load");
            return;
        }

        var directory = _fileSystem.DirectoryInfo.New(path);

        if (rulesChanged && !directoryChanged)
        {
            // Every approval a rule gave goes with the rules. The user vouched for the rule and not
            // for the two thousand files it touched, so fixing one greenlit by mistake has to undo
            // its work. What they answered one at a time is untouched.
            await _libraryIndex.OpenAsync(token);
            await _libraryIndex.RevokeRuleApprovalsAsync(token);
            await LoadDirectoryAsync(directory, reread: true, token);
            return;
        }

        if (directoryChanged)
        {
            await LoadDirectoryAsync(directory, reread: rulesChanged, token);
            return;
        }

        // Only the dance rule moved. Nothing about the files is different, only what the gate is
        // willing to let through.
        await RefreshLibraryAsync(token);
    }

    public IObservable<IChangeSet<Track>> Connect() => _tracks.Connect();

    public IObservable<IChangeSet<Track>> Connect(IObservable<string> searchText) =>
        _tracks.Connect()
            .Filter(searchText.Select(TrackSearchFilter.For));

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        // Set first: a load still in flight checks this before publishing a watcher.
        _disposed = true;

        if (disposing)
        {
            var cancellation = Interlocked.Exchange(ref _loadCts, null);
            cancellation?.Cancel();
            cancellation?.Dispose();

            _danceListSubscription.Dispose();
            _watcherSubscription.Dispose();
            _watcher.Dispose();
            _tracks.Dispose();
            _isLoading.Dispose();
            _inReviewCount.Dispose();
            _unavailableCount.Dispose();
            _loadGate.Dispose();
        }
    }

    /// <summary>What a walk of the music directory found, and where it can vouch for that.</summary>
    /// <param name="Files">Every audio file the walk actually saw.</param>
    /// <param name="DirectoriesWithMusic">
    /// The folders that were read and gave up at least one audio file. A folder that read back
    /// empty is not among them: emptied and not-mounted-yet look identical from here, so an empty
    /// read proves nothing either way.
    /// </param>
    /// <param name="UnreadableDirectories">
    /// The folders that would not open, and what the filesystem said. Kept so the question put to
    /// the user can quote the reason rather than speculate about it.
    /// </param>
    private sealed record LibraryWalk(
        ICollection<IFileInfo> Files,
        IReadOnlySet<string> DirectoriesWithMusic,
        IReadOnlyDictionary<string, string> UnreadableDirectories);

    /// <summary>Walks the music directory a folder at a time, remembering which ones it could read.</summary>
    /// <remarks>
    /// A folder at a time rather than <see cref="SearchOption.AllDirectories"/>, because the point
    /// is not only the files: it is knowing which folders the walk can speak for. One recursive
    /// enumeration reports a subtree that would not open and a subtree that is genuinely empty as
    /// the same nothing, and reconciling on that is what deletes an unmounted library.
    /// </remarks>
    private LibraryWalk DiscoverFiles(IDirectoryInfo directory)
    {
        var files = new List<IFileInfo>();
        var withMusic = new HashSet<string>(StringComparer.Ordinal);
        var unreadable = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new Queue<IDirectoryInfo>();
        pending.Enqueue(directory);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            IFileInfo[] here;
            IDirectoryInfo[] below;

            try
            {
                here = [.. current.EnumerateFiles().Where(file => SupportedAudioFormats.IsSupported(file.Name))];
                below = [.. current.EnumerateDirectories()];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A folder that would not open says nothing about what is under it. Walked past
                // rather than reported as empty, which is what would cost every row below it.
                _ = _loggerService.WarningAsync(
                    $"Could not read '{current.FullName}': {exception.Message}");
                unreadable[current.FullName] = exception.Message;
                continue;
            }

            if (here.Length > 0)
            {
                files.AddRange(here);
                withMusic.Add(current.FullName);
            }

            foreach (var child in below)
            {
                pending.Enqueue(child);
            }
        }

        return new LibraryWalk(files, withMusic, unreadable);
    }

    private async Task LoadDirectoryAsync(IDirectoryInfo directory, bool reread, CancellationToken cancellationToken)
    {
        _ = _loggerService.DebugAsync($"LoadDirectoryAsync called for '{directory.FullName}'");

        await _loadGate.WaitAsync(CancellationToken.None);
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Superseded while queued behind the previous load: leave its results alone.
                return;
            }

            await LoadDirectoryCoreAsync(directory, reread, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded mid-flight by a newer load. The expected end of this one, not a failure,
            // and the load that replaced it owns the result.
            _ = _loggerService.DebugAsync($"Load of '{directory.FullName}' was superseded");
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task LoadDirectoryCoreAsync(IDirectoryInfo directory, bool reread, CancellationToken cancellationToken)
    {
        _watcher.Stop();
        _tracks.Clear();
        _musicRoot = directory;

        if (!directory.Exists)
        {
            // Not a reason to stop: a mount point that has not mounted can be gone altogether, and
            // what the index holds under it is exactly what must not be thrown away unasked.
            _ = _loggerService.WarningAsync(
                $"Music directory '{directory.FullName}' does not exist.");
        }

        _isLoading.OnNext(true);
        try
        {
            await _libraryIndex.OpenAsync(cancellationToken);
            // The whole table in one query. Every file is then answered from memory, which is what
            // lets an unchanged startup open no audio files at all.
            var known = await _libraryIndex.SnapshotByPathAsync(cancellationToken);

            var walk = DiscoverFiles(directory);
            var audioFiles = walk.Files;
            _ = _loggerService.DebugAsync($"Found {audioFiles.Count} audio files to load");

            // Asked before a single row is written, so answering "exit" really does leave the index
            // as it was. Everything the question needs is already in hand: what the walk found, and
            // what the index holds.
            var missing = MissingFolders.Detect(
                known.Keys, walk.DirectoriesWithMusic, walk.UnreadableDirectories, directory.FullName);
            IReadOnlyCollection<string> keptUnavailable = [];

            if (missing.Count > 0)
            {
                await _loggerService.WarningAsync(
                    $"{missing.Count:N0} folders hold indexed tracks and no music was found in them; asking");

                switch (await _missingFolderPrompt.AskAsync(missing, cancellationToken))
                {
                    case MissingFolderAnswer.Exit:
                        await _loggerService.InfoAsync(
                            "Library scan abandoned at the user's word; the index is untouched");
                        return;
                    case MissingFolderAnswer.KeepThem:
                        keptUnavailable = MissingFolders.PathsIn(known.Keys, missing);
                        break;
                    case MissingFolderAnswer.ForgetThem:
                    default:
                        break;
                }
            }

            var scanned = new ConcurrentBag<ScannedFile>();
            // Everything that has been read, kept for the folder pass once the scan is complete.
            var written = new List<ScannedFile>();
            var loaded = audioFiles.ToObservable()
                .TakeUntil(_ => cancellationToken.IsCancellationRequested)
                .Select(file => LoadTrackObservable(file, directory, known, scanned, reread))
                .Merge(MaxAmountOfFileReaderThreads)
                .Buffer(TimeSpan.FromMilliseconds(200), 50);

            // Nothing is published from here. What a scan produces is derived, and derived is not
            // the same as in the library: the list is rebuilt through the gate once the scan and its
            // approvals are on disk.
            await loaded.Where(batch => batch.Any()).ForEachAsync(tracksBatch =>
            {
                // Written as the scan goes rather than all at the end. Indexing a large library on
                // a network mount takes minutes, and a run that is interrupted at 78% must not
                // throw away 78% of the work: incremental upserts are the reason this is a database
                // and not a file.
                var pending = new List<ScannedFile>();
                while (scanned.TryTake(out var entry))
                {
                    pending.Add(entry);
                    written.Add(entry);
                }

                if (pending.Count > 0)
                {
                    _libraryIndex.WriteAsync([.. pending.Select(ScannedFileMapping.ToEntry)], cancellationToken).SafeFireAndForget(exception =>
                        _loggerService.ErrorAsync("Failed to write a batch to the library index", exception));

                    _libraryIndex.ApproveAsync([.. pending.SelectMany(ScannedFileMapping.ByRuleApprovals)], cancellationToken)
                        .SafeFireAndForget(exception =>
                            _loggerService.ErrorAsync("Failed to record what the rules approved", exception));
                }

                _ = _loggerService.DebugAsync($"Added batch of '{tracksBatch.Count:N0}' tracks");
            }, cancellationToken);

            // Folder agreement runs once the folders are complete, because "what the rest of this
            // folder turned out to be" is not knowable while the folder is still being read.
            // Anything the last batch left behind.
            while (scanned.TryTake(out var remaining))
            {
                written.Add(remaining);
            }

            var rescued = FolderAgreement.Apply(written, known, directory.FullName, _danceListStore.Index, _declared);
            if (rescued > 0)
            {
                await _loggerService.DebugAsync($"Folder agreement resolved {rescued:N0} more tracks");
            }

            // Written again, because folder agreement changed some of them after the fact.
            await _libraryIndex.WriteAsync([.. written.Select(ScannedFileMapping.ToEntry)], cancellationToken);
            await _libraryIndex.ApproveAsync([.. written.SelectMany(ScannedFileMapping.ByRuleApprovals)], cancellationToken);
            await _libraryIndex.DeleteMissingAsync(
                [.. audioFiles.Select(file => file.FullName)], keptUnavailable, cancellationToken);

            // Watching starts before the library is published, so a file dropped in during the last
            // moments of a scan is noticed rather than waiting for the next start.
            if (!cancellationToken.IsCancellationRequested && !_disposed && directory.Exists)
            {
                // Not on a store that is going away, and not for a load that has been superseded.
                _watcher.Watch(directory);
            }

            await RebuildFromIndexAsync(cancellationToken);

            await _loggerService.DebugAsync(
                $"Loaded '{_tracks.Count:N0}' tracks into the library, {written.Count:N0} files read from disk");
        }
        finally
        {
            _isLoading.OnNext(false);
        }
    }

    /// <summary>
    /// Rebuilds the published list from the index, through the gate.
    /// </summary>
    /// <remarks>
    /// The library is what a person has agreed to, not what a scan derived, so this is the only
    /// place tracks are published. It opens no audio files: everything it needs is in the index,
    /// which is what lets an approval show up in the library the moment it is given.
    /// </remarks>
    public async Task RefreshLibraryAsync(CancellationToken cancellationToken = default)
    {
        // The same gate a load takes, and for the same reason. A rebuild clears the published list
        // and refills it from the index; so does a scan, in streaming batches. Ungated, an approval
        // given from the review screen while a scan was running interleaved with it, and the library
        // was left holding whichever of the two finished writing last.
        //
        // Waited on with None rather than the caller's token, so a superseded rebuild leaves the
        // gate the way it found it instead of throwing out of the wait. Callers that already hold
        // the gate call RebuildFromIndexAsync directly, so this never reenters.
        await _loadGate.WaitAsync(CancellationToken.None);
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Superseded while queued behind a load. That load rebuilds from the same index as
                // its last step, so there is nothing here left to do.
                return;
            }

            await RebuildFromIndexAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _ = _loggerService.DebugAsync("Library rebuild was superseded");
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task RebuildFromIndexAsync(CancellationToken cancellationToken)
    {
        var entries = await _libraryIndex.SnapshotByPathAsync(cancellationToken);
        var approvals = await _libraryIndex.ApprovalsAsync(cancellationToken);
        var dances = _danceListStore.Index;

        // Rows kept because a folder could not be read are not part of the library: nothing about
        // them can be played, queued or answered until their files are back. They are counted
        // instead, so the state is never mistaken for a library that is simply smaller than it was.
        var reachable = entries.Values.Where(entry => entry.IsAvailable).ToList();
        _unavailableCount.OnNext(entries.Count - reachable.Count);

        var inLibrary = new List<Track>();
        foreach (var entry in reachable)
        {
            var review = ReviewGate.Evaluate(
                entry,
                approvals.GetValueOrDefault(LibraryKey.For(entry.ContentHash), []),
                dances,
                _allowDancesOutsideTheList);

            if (review.IsInLibrary)
            {
                // A track let in on a dance the list does not carry keeps the name it was given and
                // has no slug, because a slug is the list's to hand out. It plays and it is searched
                // for like any other; a random pick cannot reach it, since that draws by tag.
                inLibrary.Add(new Track(
                    review.DanceSlug is { } slug ? dances.DisplayNameFor(slug) : review.Dance.Value ?? string.Empty,
                    review.Artist.Value ?? string.Empty,
                    review.Title.Value ?? string.Empty,
                    _fileSystem.FileInfo.New(entry.Path),
                    entry.Duration,
                    entry.Format)
                {
                    OriginalDance = entry.OriginalDance ?? string.Empty,
                    DanceSlug = review.DanceSlug
                });
            }
        }

        _tracks.Edit(list =>
        {
            list.Clear();
            list.AddRange(inLibrary);
        });

        // In the library or in review, never both: what the gate held back IS the review count,
        // whichever of its three reasons applied.
        _inReviewCount.OnNext(reachable.Count - inLibrary.Count);
    }

    private IObservable<Track> LoadTrackObservable(
        IFileInfo file, IDirectoryInfo root,
        IReadOnlyDictionary<string, LibraryEntry> known, ConcurrentBag<ScannedFile> scanned, bool reread)
    {
        // Defer keeps the inner observable cold so Merge(MaxAmountOfFileReaderThreads)
        // actually caps how many files are opened at once.
        return Observable
            .Defer(() => Observable.Start(() => LoadTrack(file, root, known, scanned, reread), TaskPoolScheduler.Default))
            .Catch<Track, Exception>(exception =>
            {
                _ = _loggerService.WarningAsync($"Error loading {file.FullName}: {exception.Message}");
                return Observable.Empty<Track>();
            });
    }

    /// <summary>
    /// Builds a track from the index when the file has not changed, and reads it when it has.
    /// </summary>
    /// <remarks>
    /// Size and write time are the whole check. Hashing would be a better answer and is what the
    /// row is keyed by, but it means opening the file, which is the cost this exists to avoid.
    /// </remarks>
    private Track LoadTrack(
        IFileInfo file, IDirectoryInfo root,
        IReadOnlyDictionary<string, LibraryEntry> known, ConcurrentBag<ScannedFile> scanned, bool reread)
    {
        // A re-read is asked for when the rules changed, and what the index holds was derived under
        // the old ones, so the shortcut is exactly what must not fire.
        if (!reread
            && known.TryGetValue(file.FullName, out var entry)
            && entry.FileSize == file.Length
            && entry.LastWriteUtc == file.LastWriteTimeUtc)
        {
            return new Track(
                entry.DanceSlug is null
                    ? entry.OriginalDance ?? string.Empty
                    : _danceListStore.Index.DisplayNameFor(entry.DanceSlug),
                entry.Artist ?? string.Empty,
                entry.Title ?? string.Empty,
                file,
                entry.Duration,
                entry.Format)
            {
                OriginalDance = entry.OriginalDance ?? string.Empty,
                DanceSlug = entry.DanceSlug
            };
        }

        var evidence = _discoveryService.Gather(file, root);
        var resolution = TrackInformationResolver.Resolve(evidence, _danceListStore.Index, _declared);

        scanned.Add(new ScannedFile(file, evidence, resolution));
        return ToTrack(file, evidence, resolution);
    }

    private Track ToTrack(IFileInfo file, TrackEvidence evidence, TrackResolution resolution) =>
        new(
            resolution.DanceSlug is null
                ? resolution.OriginalDance ?? string.Empty
                : _danceListStore.Index.DisplayNameFor(resolution.DanceSlug),
            resolution.Artist,
            resolution.Title,
            file,
            evidence.Duration,
            evidence.Format)
        {
            OriginalDance = resolution.OriginalDance ?? string.Empty,
            DanceSlug = resolution.DanceSlug
        };

    /// <summary>Decides what a change the watcher noticed is worth.</summary>
    /// <remarks>
    /// The watcher reports everything under the music directory; which of it matters is this
    /// store's business, not the watcher's.
    /// </remarks>
    private void OnFileChanged(LibraryFileChange change)
    {
        switch (change.Kind)
        {
            case LibraryFileChangeKind.Appeared:
                OnFileCreated(change.Path);
                break;
            case LibraryFileChangeKind.Vanished:
                OnFileDeleted(change.Path);
                break;
            case LibraryFileChangeKind.Renamed:
                OnFileRenamed(change.Path, change.PreviousPath!);
                break;
            default:
                break;
        }
    }

    private void OnFileCreated(string path)
    {
        if (!SupportedAudioFormats.IsSupported(path))
        {
            return;
        }

        try
        {
            IndexAndResolve(_fileSystem.FileInfo.New(path));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            _ = _loggerService.ErrorAsync(ex.Message, ex);
        }
    }

    private void OnFileDeleted(string path)
    {
        if (!SupportedAudioFormats.IsSupported(path))
        {
            return;
        }

        _fileVanished.OnNext(path);

        var track = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, path, StringComparison.Ordinal));
        if (track != null)
        {
            _tracks.Remove(track);
        }

        // The index as well as the list, or the next rebuild resurrects the deleted file. Even when
        // nothing published matched: a file still sitting in review has an index row too.
        ForgetPath(path);
    }

    private void ForgetPath(string path) =>
        Task.Run(async () =>
        {
            await _libraryIndex.DeletePathAsync(path);
            await RefreshLibraryAsync();
        }).SafeFireAndForget(exception =>
            _loggerService.ErrorAsync("Failed to forget a file the watcher saw go", exception));

    private void OnFileRenamed(string path, string previousPath)
    {
        var oldTrack = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, previousPath, StringComparison.Ordinal));
        if (oldTrack != null)
        {
            _tracks.Remove(oldTrack);
        }

        if (!SupportedAudioFormats.IsSupported(path))
        {
            // Renamed out of the formats this reads: to the index that is the file going away.
            ForgetPath(previousPath);
            return;
        }

        try
        {
            // The audio is unchanged, so its content hash and everything approved about it are too.
            // A rename is a path changing, not a track appearing.
            IndexAndResolve(_fileSystem.FileInfo.New(path), previousPath);
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            _ = _loggerService.ErrorAsync(ex.Message, ex);
        }
    }

    /// <summary>
    /// Reads a file the watcher noticed and puts it in the index, silently.
    /// </summary>
    /// <remarks>
    /// No dialog and no toast, whatever it turns out to be. The application runs in front of a room,
    /// and a tagging question during a bal is the worst possible moment to ask one.
    /// </remarks>
    private void IndexAndResolve(IFileInfo fileInfo, string? replacesPath = null)
    {
        var root = _musicRoot ?? fileInfo.Directory!;
        var evidence = _discoveryService.Gather(fileInfo, root);

        // In order and once: written, approved by whatever rule answered it, and only then does the
        // library get rebuilt, or the rebuild would run against a row that is not there yet. The
        // old path of a rename goes after the write, so the audio's hash is never unreferenced and
        // the approvals riding on it survive.
        Task.Run(async () =>
        {
            // What the rest of the folder turned out to be speaks for a dropped-in file exactly as
            // it does during a scan; the watcher path once resolved blind and the same file got a
            // different answer depending on who noticed it.
            var known = await _libraryIndex.SnapshotByPathAsync();
            var folderKey = evidence.FolderKey ?? string.Empty;
            var agreed = FolderAgreement.AgreedDanceAround(
                fileInfo.FullName, folderKey, known, root.FullName);

            var resolution = TrackInformationResolver.Resolve(evidence, _danceListStore.Index, _declared, agreed);
            var scanned = new ScannedFile(fileInfo, evidence, resolution);

            await _libraryIndex.WriteAsync([ScannedFileMapping.ToEntry(scanned)]);
            await _libraryIndex.ApproveAsync([.. ScannedFileMapping.ByRuleApprovals(scanned)]);
            if (replacesPath is not null)
            {
                await _libraryIndex.DeletePathAsync(replacesPath);
            }

            await RefreshLibraryAsync();
        }).SafeFireAndForget(exception =>
            _loggerService.ErrorAsync("Failed to index a file the watcher noticed", exception));
    }

}
