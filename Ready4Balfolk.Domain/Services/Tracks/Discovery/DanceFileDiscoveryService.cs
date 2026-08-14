using System.Collections.Concurrent;
using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class DanceFileDiscoveryService(DanceFileService danceFileService) : IDanceFileDiscoveryService
{
    // Scanned from Parallel.ForEachAsync in TrackStore, so this must be
    // thread-safe.
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = [];

    public bool Exists(IDirectoryInfo directoryInfo) => danceFileService.Exists(directoryInfo);

    public Dictionary<string, string> Matches(IDirectoryInfo directoryInfo)
    {
        if (_cache.TryGetValue(directoryInfo.FullName, out var cached))
        {
            if (danceFileService.FileInfo(directoryInfo).LastWriteTimeUtc <= cached.LastWriteTimeUtc)
            {
                return cached.DanceEntries;
            }
        }

        var danceEntries = danceFileService.Matches(directoryInfo);
        var entry = new CacheEntry(directoryInfo.FullName, danceFileService.FileInfo(directoryInfo).LastWriteTimeUtc, danceEntries);
        // Indexer, not Add: the entry replaces a stale one when dances.json
        // changed since it was cached.
        _cache[entry.FullName] = entry;
        return danceEntries;
    }

    public void Write(IDirectoryInfo directoryInfo, IEnumerable<DanceFileEntry> danceEntries) => danceFileService.Write(directoryInfo, danceEntries);

    private sealed record CacheEntry(string FullName, DateTime LastWriteTimeUtc, Dictionary<string, string> DanceEntries);
}
