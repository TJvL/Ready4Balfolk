using System.Reactive.Concurrency;
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
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public TrackStore(
        ILoggerService loggerService,
        ITrackDiscoveryService discoveryService,
        ISynonymResolutionService synonymService,
        ITrackDurationCache durationCache)
    {
        _loggerService = loggerService;
        _discoveryService = discoveryService;
        _synonymService = synonymService;
        _durationCache = durationCache;

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

    public DirectoryInfo? MusicDirectory
    {
        set
        {
            if (value is null)
            {
                return;
            }

            if (string.Equals(field?.FullName, value.FullName, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            LoadDirectoryAsync(value).SafeFireAndForget(exception => _loggerService.ErrorAsync("Loading directory failed", exception));
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

        if (disposing)
        {
            _synonymSubscription.Dispose();
            _watcher?.Dispose();
            _tracks.Dispose();
            _isLoading.Dispose();
        }

        _disposed = true;
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

    private static ICollection<FileInfo> DiscoverFiles(DirectoryInfo directory)
    {
        return [
            ..SupportedAudioFormats.Extensions
                .SelectMany(ext => directory.EnumerateFiles($"*{ext}", SearchOption.AllDirectories))
        ];
    }

    private async Task LoadDirectoryAsync(DirectoryInfo directory)
    {
        _ = _loggerService.DebugAsync($"LoadDirectoryAsync called for '{directory.FullName}'");

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
        await _durationCache.LoadAsync();

        var audioFiles = DiscoverFiles(directory);
        _ = _loggerService.DebugAsync($"Found {audioFiles.Count} audio files to load");

        var trackLoaded = audioFiles.ToObservable()
            .Select(file => Observable.Start(() =>
                {
                    var cachedDuration = _durationCache.TryGetDuration(
                        file.FullName, file.LastWriteTimeUtc);
                    if (cachedDuration.HasValue)
                    {
                        return _discoveryService.LoadTrackWithDuration(file, cachedDuration.Value);
                    }

                    var track = _discoveryService.LoadTrack(file);
                    _durationCache.SetDuration(file.FullName, file.LastWriteTimeUtc, track.Length);
                    return track;
                }, TaskPoolScheduler.Default)
                .Catch<Track, Exception>((ex) =>
                {
                    _ = _loggerService.WarningAsync($"Error loading {file.FullName}: {ex.Message}");
                    return Observable.Empty<Track>();
                }))
            .Merge(MaxAmountOfFileReaderThreads)
            .Buffer(TimeSpan.FromMilliseconds(200), 50);

        await trackLoaded.ForEachAsync(tracksBatch =>
        {
            _tracks.Edit(innerList => innerList.AddRange(tracksBatch));

            _ = _loggerService.DebugAsync($"Added batch of {tracksBatch.Count} tracks");
        });

        await _loggerService.DebugAsync($"Loaded {_tracks.Count} tracks successfully");
        await _durationCache.SaveAsync([.. audioFiles.Select(r => r.FullName)]);
        StartWatching(directory);
        _isLoading.OnNext(false);
    }

    private void StartWatching(FileSystemInfo directory)
    {
        _watcher = new FileSystemWatcher(directory.FullName)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
        };

        _watcher.Created += OnFileCreated;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;

        _watcher.EnableRaisingEvents = true;
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        if (!SupportedAudioFormats.IsSupported(fileSystemEventArgs.FullPath))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(fileSystemEventArgs.FullPath);
            var track = _discoveryService.LoadTrack(fileInfo);
            _durationCache.SetDuration(fileInfo.FullName, fileInfo.LastWriteTimeUtc, track.Length);
            _tracks.Add(ResolveTrackDance(track));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            await _loggerService.ErrorAsync(ex.Message, ex);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
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

    private async void OnFileRenamed(object sender, RenamedEventArgs renamedEventArgs)
    {
        var oldTrack = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, renamedEventArgs.OldFullPath, StringComparison.Ordinal));
        if (oldTrack != null)
        {
            _tracks.Remove(oldTrack);
        }

        if (!SupportedAudioFormats.IsSupported(renamedEventArgs.FullPath))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(renamedEventArgs.FullPath);
            var track = _discoveryService.LoadTrack(fileInfo);
            _durationCache.SetDuration(fileInfo.FullName, fileInfo.LastWriteTimeUtc, track.Length);
            _tracks.Add(ResolveTrackDance(track));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            await _loggerService.ErrorAsync(ex.Message, ex);
        }
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
