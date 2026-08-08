using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using DynamicData;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
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
    private readonly SourceList<Track> _tracks = new();
    private readonly BehaviorSubject<bool> _isLoading = new(false);
    private readonly IDisposable _danceListSubscription;
    private readonly CompositeDisposable _fileWatcherSubscriptions = [];
    // Loads are started fire-and-forget from the setter, so without a gate two of them interleave:
    // each opens by disposing the watcher and clearing the track list, so one load ends up
    // disposing the watcher the other just published, and appending its tracks after the other
    // cleared them.
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCts;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public TrackStore(
        ILoggerService loggerService,
        ITrackDiscoveryService discoveryService,
        IDanceListStore danceListStore,
        ILibraryIndex libraryIndex)
    {
        _loggerService = loggerService;
        _discoveryService = discoveryService;
        _danceListStore = danceListStore;
        _libraryIndex = libraryIndex;

        // Skip(1): the store replays its current list to a new subscriber, and re-resolving an
        // empty track list at construction is work with nothing to do.
        _danceListSubscription = danceListStore.Observe()
            .Skip(1)
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(_ => ReResolveAllTracks());
    }

    ~TrackStore()
    {
        Dispose(false);
    }

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public IReadOnlyList<Track> Current => _tracks.Items.ToList();

    public DirectoryInfo? MusicDirectory
    {
        set
        {
            if (value is null)
            {
                _ = _loggerService.DebugAsync("Set null value");
                return;
            }

            if (string.Equals(field?.FullName, value.FullName, StringComparison.Ordinal))
            {
                _ = _loggerService.DebugAsync("Same field name, don't do rediscover");
                return;
            }

            field = value;

            // Cancel whatever is in flight so it stops before it can touch shared state again.
            // Superseded sources are deliberately not disposed here: the load that owns one may
            // still be observing its token, and a CancellationTokenSource without registered
            // timers holds nothing worth reclaiming.
            var cancellation = new CancellationTokenSource();
            Interlocked.Exchange(ref _loadCts, cancellation)?.Cancel();

            Task.Run(() => LoadDirectoryAsync(value, cancellation.Token)).SafeFireAndForget(exception => _loggerService.ErrorAsync("Loading directory failed", exception));
        }
    }

    public IObservable<IChangeSet<Track>> Connect() => _tracks.Connect();

    public IObservable<IChangeSet<Track>> Connect(IObservable<string> searchText) =>
        _tracks.Connect()
            .Filter(searchText.Select(CreateSearchFilter));

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
            _fileWatcherSubscriptions.Dispose();
            _watcher?.Dispose();
            _tracks.Dispose();
            _isLoading.Dispose();
            _loadGate.Dispose();
        }
    }

    private void ReResolveAllTracks()
    {
        _tracks.Edit(list =>
        {
            for (var i = 0; i < list.Count; i++)
            {
                list[i] = ResolveTrackDance(list[i]);
            }
        });
    }

    /// <summary>
    /// Points a track at a dance in the list, or leaves it unresolved.
    /// </summary>
    /// <remarks>
    /// An unknown name is kept as it stands rather than blanked: it is what the tagging editor
    /// later groups by, and a track the list has nothing to say about is still a track.
    /// </remarks>
    private Track ResolveTrackDance(Track track)
    {
        var index = _danceListStore.Index;
        var slug = index.ResolveSlug(track.OriginalDance);

        return track with
        {
            Dance = slug is null ? track.OriginalDance : index.DisplayNameFor(slug),
            OriginalDance = track.OriginalDance,
            DanceSlug = slug
        };
    }

    private static ICollection<FileInfo> DiscoverFiles(DirectoryInfo directory)
    {
        return
        [
            ..SupportedAudioFormats.Extensions
                .SelectMany(ext => directory.EnumerateFiles($"*{ext}", SearchOption.AllDirectories))
        ];
    }

    private async Task LoadDirectoryAsync(DirectoryInfo directory, CancellationToken cancellationToken)
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

            await LoadDirectoryCoreAsync(directory, cancellationToken);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task LoadDirectoryCoreAsync(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        _watcher = null;
        _tracks.Clear();

        if (!directory.Exists)
        {
            _ = _loggerService.WarningAsync(
                $"Music directory '{directory.FullName}' does not exist.");
            return;
        }

        _isLoading.OnNext(true);
        try
        {
            await _libraryIndex.OpenAsync(cancellationToken);
            // The whole table in one query. Every file is then answered from memory, which is what
            // lets an unchanged startup open no audio files at all.
            var known = await _libraryIndex.SnapshotByPathAsync(cancellationToken);

            var audioFiles = DiscoverFiles(directory);
            _ = _loggerService.DebugAsync($"Found {audioFiles.Count} audio files to load");

            var scanned = new ConcurrentBag<LibraryEntry>();
            var loaded = audioFiles.ToObservable()
                .TakeUntil(_ => cancellationToken.IsCancellationRequested)
                .Select(file => LoadTrackObservable(file, known, scanned))
                .Merge(MaxAmountOfFileReaderThreads)
                .Buffer(TimeSpan.FromMilliseconds(200), 50);

            await loaded.Where(batch => batch.Any()).ForEachAsync(tracksBatch =>
            {
                _tracks.Edit(innerList => innerList.AddRange(tracksBatch));

                _ = _loggerService.DebugAsync($"Added batch of '{tracksBatch.Count:N0}' tracks");
            }, cancellationToken);

            await _loggerService.DebugAsync(
                $"Loaded '{_tracks.Count:N0}' tracks, {scanned.Count:N0} of them read from disk");

            await _libraryIndex.WriteAsync([.. scanned], cancellationToken);
            await _libraryIndex.DeleteMissingAsync([.. audioFiles.Select(file => file.FullName)], cancellationToken);

            StartWatching(directory, cancellationToken);
        }
        finally
        {
            _isLoading.OnNext(false);
        }
    }

    private IObservable<Track> LoadTrackObservable(
        FileInfo file, IReadOnlyDictionary<string, LibraryEntry> known, ConcurrentBag<LibraryEntry> scanned)
    {
        // Defer keeps the inner observable cold so Merge(MaxAmountOfFileReaderThreads)
        // actually caps how many files are opened at once.
        return Observable
            .Defer(() => Observable.Start(() => LoadTrack(file, known, scanned), TaskPoolScheduler.Default))
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
        FileInfo file, IReadOnlyDictionary<string, LibraryEntry> known, ConcurrentBag<LibraryEntry> scanned)
    {
        if (known.TryGetValue(file.FullName, out var entry)
            && entry.FileSize == file.Length
            && entry.LastWriteUtc == file.LastWriteTimeUtc)
        {
            return ResolveTrackDance(new Track(
                entry.OriginalDance ?? string.Empty,
                entry.Artist ?? string.Empty,
                entry.Title ?? string.Empty,
                file,
                entry.Duration,
                entry.Format));
        }

        var scan = _discoveryService.Scan(file);
        var track = ResolveTrackDance(new Track(
            scan.Dance, scan.Artist, scan.Title, file, scan.Duration, scan.Format));

        scanned.Add(new LibraryEntry
        {
            ContentHash = scan.ContentHash,
            Path = file.FullName,
            FileSize = file.Length,
            LastWriteUtc = file.LastWriteTimeUtc,
            Duration = scan.Duration,
            Format = scan.Format,
            DanceSlug = track.DanceSlug,
            OriginalDance = track.OriginalDance,
            Artist = track.Artist,
            Title = track.Title
        });

        return track;
    }

    private void StartWatching(FileSystemInfo directory, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _disposed)
        {
            // Do not publish a watcher nobody will dispose, and do not enable one on a store that
            // is going away: enabling a disposed watcher is what threw ObjectDisposedException.
            return;
        }

        _fileWatcherSubscriptions.Clear();
        // Local capture: the FromEventPattern remove-handlers must close over this
        // instance, not the _watcher field, which is nulled on the next reload
        // before the old subscriptions are disposed.
        var watcher = new FileSystemWatcher(directory.FullName)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
        };
        _watcher = watcher;

        var createdObs = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => watcher.Created += h,
                h => watcher.Created -= h
            )
                .Select(fromEvent => OnFileCreated(fromEvent.EventArgs))
                .Where(r => r != null)
                .Subscribe(track => _tracks.Edit(e => e.Add(track!)));
        _fileWatcherSubscriptions.Add(createdObs);

        var deletedObs = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => watcher.Deleted += h,
                h => watcher.Deleted -= h
            )
            .Subscribe(fromEvent => OnFileDeleted(fromEvent.EventArgs));
        _fileWatcherSubscriptions.Add(deletedObs);

        var renamedObs = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                    h => watcher.Renamed += h,
                    h => watcher.Renamed -= h
                )
                .Select(evt => OnFileRenamed(evt.EventArgs))
                .Where(r => r != null)
                .Subscribe(track => _tracks.Edit(e => e.Add(track!)));
        _fileWatcherSubscriptions.Add(renamedObs);

        watcher.EnableRaisingEvents = true;
    }

    private Track? OnFileCreated(FileSystemEventArgs fileSystemEventArgs)
    {
        if (!SupportedAudioFormats.IsSupported(fileSystemEventArgs.FullPath))
        {
            return null;
        }

        try
        {
            return IndexAndResolve(new FileInfo(fileSystemEventArgs.FullPath));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            _ = _loggerService.ErrorAsync(ex.Message, ex);
        }

        return null;
    }

    private void OnFileDeleted(FileSystemEventArgs fileSystemEventArgs)
    {
        if (!SupportedAudioFormats.IsSupported(fileSystemEventArgs.FullPath))
        {
            return;
        }

        var track = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, fileSystemEventArgs.FullPath, StringComparison.Ordinal));
        if (track != null)
        {
            _tracks.Remove(track);
        }
    }

    private Track? OnFileRenamed(RenamedEventArgs renamedEventArgs)
    {
        var oldTrack = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, renamedEventArgs.OldFullPath, StringComparison.Ordinal));
        if (oldTrack != null)
        {
            _tracks.Remove(oldTrack);
        }

        if (!SupportedAudioFormats.IsSupported(renamedEventArgs.FullPath))
        {
            return null;
        }

        try
        {
            return IndexAndResolve(new FileInfo(renamedEventArgs.FullPath));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            _ = _loggerService.ErrorAsync(ex.Message, ex);
        }

        return null;
    }

    /// <summary>
    /// Reads a file the watcher noticed and puts it in the index, silently.
    /// </summary>
    /// <remarks>
    /// No dialog and no toast, whatever it turns out to be. The application runs in front of a room,
    /// and a tagging question during a bal is the worst possible moment to ask one.
    /// </remarks>
    private Track IndexAndResolve(FileInfo fileInfo)
    {
        var scan = _discoveryService.Scan(fileInfo);
        var track = ResolveTrackDance(new Track(
            scan.Dance, scan.Artist, scan.Title, fileInfo, scan.Duration, scan.Format));

        _libraryIndex.WriteAsync([
            new LibraryEntry
            {
                ContentHash = scan.ContentHash,
                Path = fileInfo.FullName,
                FileSize = fileInfo.Length,
                LastWriteUtc = fileInfo.LastWriteTimeUtc,
                Duration = scan.Duration,
                Format = scan.Format,
                DanceSlug = track.DanceSlug,
                OriginalDance = track.OriginalDance,
                Artist = track.Artist,
                Title = track.Title
            }
        ]).SafeFireAndForget(exception =>
            _loggerService.ErrorAsync("Failed to index a new file", exception));

        return track;
    }

    private static Func<Track, bool> CreateSearchFilter(string search)
    {
        var normalized = StringNormalizer.Normalize(search);
        return string.IsNullOrEmpty(normalized)
            ? _ => true
            : track =>
                StringNormalizer.Normalize(track.Dance).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.OriginalDance).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.Artist).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.Title).Contains(normalized, StringComparison.Ordinal);
    }
}
