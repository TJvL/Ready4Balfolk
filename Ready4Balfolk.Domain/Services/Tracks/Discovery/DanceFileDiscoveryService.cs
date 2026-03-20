using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class DanceFileService
{
    private readonly IFileSystem _fileSystem;
    private readonly JsonSerializerOptions _serializationOption = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public DanceFileService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool Exists(IDirectoryInfo directoryInfo) => FileInfo(directoryInfo).Exists;

    public Dictionary<string, string> Matches(IDirectoryInfo directoryInfo)
    {
        var fileInfo = FileInfo(directoryInfo);
        if (!fileInfo.Exists)
        {
            return [];
        }

        using var stream = fileInfo.OpenRead();
        // Deserialize JSON from the stream
        var entries = JsonSerializer.Deserialize<DanceFileEntry[]>(stream);

        if (entries?.Length == 0)
        {
            WriteEmptyTemplate(directoryInfo);
            return [];
        }

        return entries?.ToDictionary(r => r.FileName, r => r.Dance) ?? [];
    }

    void WriteEmptyTemplate(IDirectoryInfo directoryInfo)
    {
        var supportedFormats = AudioFormatInformation.SupportedFormats;

        var discoveredMusic = directoryInfo.EnumerateFiles()
            .Where(r => supportedFormats.Contains(r.Extension))
            .Select(r => new DanceFileEntry(r.Name));

        Write(directoryInfo, discoveredMusic);
    }

    public IFileInfo FileInfo(IDirectoryInfo directoryInfo)
    {
        if (!directoryInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory '{directoryInfo.FullName}' does not exist");
        }

        var filePath = _fileSystem.Path.Combine(directoryInfo.FullName, "dances.json");

        return _fileSystem.FileInfo.New(filePath);
    }

    public void Write(IDirectoryInfo directoryInfo, IEnumerable<DanceFileEntry> danceEntries)
    {
        var fileInfo = FileInfo(directoryInfo);

        // Write text to the file
        using var stream = fileInfo.Create();
        JsonSerializer.Serialize(stream, danceEntries, _serializationOption);
    }
}

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
        _cache.Add(directoryInfo.FullName, new CacheEntry(directoryInfo.FullName, _danceFileService.FileInfo(directoryInfo).LastWriteTimeUtc,  danceEntries));
        return danceEntries;
    }

    public void Write(IDirectoryInfo directoryInfo, IEnumerable<DanceFileEntry> danceEntries) => _danceFileService.Write(directoryInfo, danceEntries);

    private sealed record CacheEntry(string FilePath, DateTime LastWriteTimeUtc, Dictionary<string, string> DanceEntries);
}
