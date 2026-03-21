using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class DanceFileDiscoveryService : IDanceFileDiscoveryService
{
    private readonly IDictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
    private readonly DanceFileService _danceFileService;

    public DanceFileDiscoveryService(DanceFileService danceFileService)
    {
        _danceFileService = danceFileService;
    }

    public bool Exists(IDirectoryInfo directoryInfo) => _danceFileService.Exists(directoryInfo);

    public Dictionary<string, string> Matches(IDirectoryInfo directoryInfo)
    {
        if (_cache.TryGetValue(directoryInfo.FullName, out var cached))
        {
            if (_danceFileService.FileInfo(directoryInfo).LastWriteTimeUtc <= cached.LastWriteTimeUtc)
            {
                return cached.DanceEntries;
            }
        }

        var danceEntries = _danceFileService.Matches(directoryInfo);
        var entry = new CacheEntry(directoryInfo.FullName, _danceFileService.FileInfo(directoryInfo).LastWriteTimeUtc, danceEntries);
        _cache.Add(entry.FullName, entry);
        return danceEntries;
    }

    public void Write(IDirectoryInfo directoryInfo, IEnumerable<DanceFileEntry> danceEntries) => _danceFileService.Write(directoryInfo, danceEntries);

    private sealed record CacheEntry(string FullName, DateTime LastWriteTimeUtc, Dictionary<string, string> DanceEntries);
}
