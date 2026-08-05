using System.Collections.Concurrent;
using System.Text.Json;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class TrackDurationCache(IApplicationSettingsDirectory dataDirectory, ILoggerService loggerService) : ITrackDurationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath = Path.Combine(dataDirectory.DirectoryInfoRoot.FullName, "track_duration_cache.json");
    private ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _entries = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync<List<CacheEntry>>(stream);

            var dict = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
            if (list is not null)
            {
                foreach (var entry in list)
                {
                    dict[entry.FilePath] = entry;
                }
            }

            _entries = dict;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _ = loggerService.WarningAsync($"Corrupt duration cache, rebuilding: {ex.Message}");
            _entries = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
        }
    }

    public TimeSpan? TryGetDuration(string filePath, DateTime lastWriteTimeUtc) =>
        _entries.TryGetValue(filePath, out var entry) && entry.LastWriteTimeUtc == lastWriteTimeUtc
            ? entry.Duration
            : null;

    public void SetDuration(string filePath, DateTime lastWriteTimeUtc, TimeSpan duration) =>
        _entries[filePath] = new CacheEntry(filePath, lastWriteTimeUtc, duration);

    public async Task SaveAsync(HashSet<string> existingFilePaths)
    {
        var keysToRemove = _entries.Keys.Where(k => !existingFilePaths.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            _entries.TryRemove(key, out _);
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _entries.Values.ToList(), JsonOptions);
            _ = loggerService.DebugAsync($"Duration cache saved ({_entries.Count} entries)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = loggerService.ErrorAsync("Failed to save duration cache", ex);
        }
    }

    private sealed record CacheEntry(string FilePath, DateTime LastWriteTimeUtc, TimeSpan Duration);
}
