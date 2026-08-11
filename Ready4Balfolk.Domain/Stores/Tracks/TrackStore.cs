using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using DynamicData;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Synonym;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public sealed class TrackStore : ITrackStore, IDisposable
{
    private const int MaxAmountOfFileReaderThreads = 32;
    private readonly ILoggerService _loggerService;
    private readonly ITrackDiscoveryService _discoveryService;
    private readonly ISynonymResolutionService _synonymService;
    private readonly ITrackDurationCache _durationCache;
    private readonly SourceList<Track> _tracks = new();
    private readonly BehaviorSubject<bool> _isLoading = new(false);
    private readonly IDisposable _synonymSubscription;
    private readonly CompositeDisposable _fileWatcherSubscriptions = [];
    // Loads are started fire-and-forget from the setter, so without a gate two of them interleave:
    // each opens by disposing the watcher and clearing the track list, so one load ends up
    // disposing the watcher the other just published, and appending its tracks after the other
    // cleared them.
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCts;
    private IFileSystemWatcher? _watcher;
    private bool _disposed;
    private readonly IFileSystem _fileSystem;

    public TrackStore(
        ILoggerService loggerService,
        ITrackDiscoveryService discoveryService,
        ISynonymResolutionService synonymService,
        ITrackDurationCache durationCache,
        IFileSystem fileSystem)
    {
        _loggerService = loggerService;
        _discoveryService = discoveryService;
        _synonymService = synonymService;
        _durationCache = durationCache;
        _fileSystem = fileSystem;

        _synonymSubscription = synonymService.Changed
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(_ => ReResolveAllTracks());
    }

    ~TrackStore()
    {
        Dispose(false);
    }

    public IObservable<bool> IsLoading => _isLoading.AsObservable();

    public IReadOnlyList<Track> Current => _tracks.Items.ToList();

    public IDirectoryInfo? MusicDirectory
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

            _synonymSubscription.Dispose();
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

    private Track ResolveTrackDance(Track track)
    {
        var resolved = _synonymService.Resolve(track.OriginalDance);
        return track with
        {
            Dance = resolved,
            OriginalDance = track.OriginalDance
        };
    }

    private static ICollection<IFileInfo> DiscoverFiles(IDirectoryInfo directory)
    {
        return
        [
            ..SupportedAudioFormats.Extensions
                .SelectMany(ext => directory.EnumerateFiles($"*{ext}", SearchOption.AllDirectories))
        ];
    }

    private async Task LoadDirectoryAsync(IDirectoryInfo directory, CancellationToken cancellationToken)
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

    private async Task LoadDirectoryCoreAsync(IDirectoryInfo directory, CancellationToken cancellationToken)
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
            await _durationCache.LoadAsync();

            var audioFiles = DiscoverFiles(directory);
            _ = _loggerService.DebugAsync($"Found {audioFiles.Count} audio files to load");

            var trackLoaded = audioFiles.ToObservable()
                .TakeUntil(_ => cancellationToken.IsCancellationRequested)
                .Select(LoadTrackObservable)
                .Merge(MaxAmountOfFileReaderThreads)
                .Buffer(TimeSpan.FromMilliseconds(200), 50);

            await trackLoaded
                .Where(r => r.Any()).ForEachAsync(tracksBatch =>
            {
                _tracks.Edit(innerList => innerList.AddRange(tracksBatch));

                _ = _loggerService.DebugAsync($"Added batch of '{tracksBatch.Count:N0}' tracks");
            }, cancellationToken);

            await _loggerService.DebugAsync($"Loaded '{_tracks.Count:N0}' tracks successfully");
            await _durationCache.SaveAsync([.. audioFiles.Select(r => r.FullName)]);
            StartWatching(directory, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // this can happen when the ForEachAsync takes too long
        }
        finally
        {
            _isLoading.OnNext(false);
        }
    }

    private IObservable<Track> LoadTrackObservable(IFileInfo file)
    {
        // Defer keeps the inner observable cold so Merge(MaxAmountOfFileReaderThreads)
        // actually caps how many LoadTrack calls run concurrently.
        return Observable
            .Defer(() => Observable.Start(() => LoadTrack(file), TaskPoolScheduler.Default))
            .Catch<Track, Exception>((ex) =>
            {
                _ = _loggerService.WarningAsync($"Error loading {file.FullName}: {ex.Message}");
                return Observable.Empty<Track>();
            });
    }

    private Track LoadTrack(IFileInfo file)
    {
        var cachedDuration = _durationCache.TryGetDuration(file.FullName, file.LastWriteTimeUtc);
        if (cachedDuration.HasValue)
        {
            return _discoveryService.LoadTrackWithDuration(file, cachedDuration.Value);
        }

        var track = _discoveryService.LoadTrack(file);
        _durationCache.SetDuration(file.FullName, file.LastWriteTimeUtc, track.Length);
        return track;
    }

    private void StartWatching(IFileSystemInfo directory, CancellationToken cancellationToken)
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

        var watcher = _fileSystem.FileSystemWatcher.New(directory.FullName);
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

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
            var fileInfo = _fileSystem.FileInfo.New(fileSystemEventArgs.FullPath);
            var track = _discoveryService.LoadTrack(fileInfo);
            _durationCache.SetDuration(fileInfo.FullName, fileInfo.LastWriteTimeUtc, track.Length);
            return ResolveTrackDance(track);
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
            var fileInfo = _fileSystem.FileInfo.New(renamedEventArgs.FullPath);
            var track = _discoveryService.LoadTrack(fileInfo);
            _durationCache.SetDuration(fileInfo.FullName, fileInfo.LastWriteTimeUtc, track.Length);
            return ResolveTrackDance(track);
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            _ = _loggerService.ErrorAsync(ex.Message, ex);
        }

        return null;
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
