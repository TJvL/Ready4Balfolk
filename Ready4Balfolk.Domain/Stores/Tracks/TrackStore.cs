using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Synonym;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

public sealed class TrackStore : ITrackStore, IDisposable
{
    private readonly ILoggerService _loggerService;
    private readonly ITrackDiscoveryService _discoveryService;
    private readonly ISynonymResolutionService _synonymService;
    private readonly SourceList<Track> _tracks = new();
    private readonly IDisposable _synonymSubscription;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public TrackStore(
        ILoggerService loggerService,
        ITrackDiscoveryService discoveryService,
        ISynonymResolutionService synonymService)
    {
        _loggerService = loggerService;
        _discoveryService = discoveryService;
        _synonymService = synonymService;

        _synonymSubscription = synonymService.Changed
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(_ => ReResolveAllTracks());
    }

    ~TrackStore() => Dispose(false);

    public IReadOnlyList<Track> Current => _tracks.Items.ToList();

    public DirectoryInfo? MusicDirectory
    {
        set
        {
            if (value is null)
                return;
            if (string.Equals(field?.FullName, value.FullName, StringComparison.Ordinal))
                return;
            field = value;
            _ = LoadDirectoryAsync(value);
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
            return;

        if (disposing)
        {
            _synonymSubscription.Dispose();
            _watcher?.Dispose();
            _tracks.Dispose();
        }

        _disposed = true;
    }

    private void ReResolveAllTracks()
    {
        _tracks.Edit(list =>
        {
            for (var i = 0; i < list.Count; i++)
                list[i] = ResolveTrackDance(list[i]);
        });
    }

    private Track ResolveTrackDance(Track track)
    {
        var resolved = _synonymService.Resolve(track.OriginalDance);
        return track with { Dance = resolved, OriginalDance = track.OriginalDance };
    }

    private async Task LoadDirectoryAsync(DirectoryInfo directory)
    {
        await _loggerService.DebugAsync($"LoadDirectoryAsync called for '{directory.FullName}'");

        _watcher?.Dispose();
        _watcher = null;
        _tracks.Clear();

        if (!directory.Exists)
        {
            await _loggerService.WarningAsync(
                $"Music directory '{directory.FullName}' does not exist.");
            return;
        }

        try
        {
            var mp3Files = directory.EnumerateFiles("*.mp3", SearchOption.AllDirectories);
            var tasks = mp3Files.Select(_discoveryService.LoadTrackAsync).ToArray();
            await _loggerService.DebugAsync($"Found {tasks.Length} mp3 files to load");

            await foreach (var completedTask in Task.WhenEach(tasks))
            {
                try
                {
                    _tracks.Add(ResolveTrackDance(await completedTask));
                }
                catch (Exception ex) when (ex is FormatException or IOException)
                {
                    await _loggerService.ErrorAsync(ex.Message, ex);
                }
            }

            await _loggerService.DebugAsync($"Loaded {_tracks.Count} tracks successfully");
            StartWatching(directory);
        }
        catch (Exception ex)
        {
            await _loggerService.ErrorAsync(
                $"Failed to load tracks from '{directory.FullName}'", ex);
        }
    }

    private void StartWatching(FileSystemInfo directory)
    {
        _watcher = new FileSystemWatcher(directory.FullName, "*.mp3")
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
        try
        {
            var track = await _discoveryService.LoadTrackAsync(new FileInfo(fileSystemEventArgs.FullPath));
            _tracks.Add(ResolveTrackDance(track));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            await _loggerService.ErrorAsync(ex.Message, ex);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        var track = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, fileSystemEventArgs.FullPath, StringComparison.Ordinal));
        if (track != null)
            _tracks.Remove(track);
    }

    private async void OnFileRenamed(object sender, RenamedEventArgs renamedEventArgs)
    {
        var oldTrack = _tracks.Items.FirstOrDefault(t =>
            string.Equals(t.FileInfo.FullName, renamedEventArgs.OldFullPath, StringComparison.Ordinal));
        if (oldTrack != null)
            _tracks.Remove(oldTrack);

        if (!Path.GetExtension(renamedEventArgs.FullPath).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var track = await _discoveryService.LoadTrackAsync(new FileInfo(renamedEventArgs.FullPath));
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
            ? (_ => true)
            : (track =>
            StringNormalizer.Normalize(track.Dance).Contains(normalized, StringComparison.Ordinal) ||
            StringNormalizer.Normalize(track.OriginalDance).Contains(normalized, StringComparison.Ordinal) ||
            StringNormalizer.Normalize(track.Artist).Contains(normalized, StringComparison.Ordinal) ||
            StringNormalizer.Normalize(track.Title).Contains(normalized, StringComparison.Ordinal));
    }
}
