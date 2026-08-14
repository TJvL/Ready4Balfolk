using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class DanceFileService(IFileSystem fileSystem, ILoggerService loggerService)
{
    private readonly JsonSerializerOptions _serializationOption = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public bool Exists(IDirectoryInfo directoryInfo) => FileInfo(directoryInfo).Exists;

    public Dictionary<string, string> Matches(IDirectoryInfo directoryInfo)
    {
        var fileInfo = FileInfo(directoryInfo);
        if (!fileInfo.Exists)
        {
            return [];
        }

        DanceFileEntry[]? entries;
        try
        {
            // The read stream is disposed at the end of this try block, before
            // WriteEmptyTemplate can rewrite the same file; Windows-style
            // sharing rules reject the overlap.
            using var stream = fileInfo.OpenRead();
            entries = JsonSerializer.Deserialize<DanceFileEntry[]>(stream);
        }
        catch (JsonException exception)
        {
            // dances.json is user-edited; a malformed file must not break the
            // directory scan.
            _ = loggerService.ErrorAsync($"Malformed dance file '{fileInfo.FullName}'", exception);
            return [];
        }

        if (entries is null || entries.Length == 0)
        {
            WriteEmptyTemplate(directoryInfo);
            return [];
        }

        var matches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FileName))
            {
                _ = loggerService.WarningAsync(
                    $"Dance file '{fileInfo.FullName}' contains an entry without a file name; it is ignored.");
                continue;
            }

            if (!matches.TryAdd(entry.FileName, entry.Dance))
            {
                _ = loggerService.WarningAsync(
                    $"Dance file '{fileInfo.FullName}' contains a duplicate entry for '{entry.FileName}'; the first entry wins.");
            }
        }

        return matches;
    }

    private void WriteEmptyTemplate(IDirectoryInfo directoryInfo)
    {
        var supportedFormats = new HashSet<string>(
            AudioFormatInformation.SupportedFormats, StringComparer.OrdinalIgnoreCase);

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

        var filePath = fileSystem.Path.Combine(directoryInfo.FullName, "dances.json");

        return fileSystem.FileInfo.New(filePath);
    }

    public void Write(IDirectoryInfo directoryInfo, IEnumerable<DanceFileEntry> danceEntries)
    {
        var fileInfo = FileInfo(directoryInfo);

        // Write text to the file
        using var stream = fileInfo.Create();
        JsonSerializer.Serialize(stream, danceEntries, _serializationOption);
    }
}
