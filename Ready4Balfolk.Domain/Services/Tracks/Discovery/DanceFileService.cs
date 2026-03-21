using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public class DanceFileService(IFileSystem fileSystem)
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

    private void WriteEmptyTemplate(IDirectoryInfo directoryInfo)
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
