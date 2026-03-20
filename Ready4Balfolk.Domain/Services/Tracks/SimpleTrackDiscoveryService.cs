using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class SimpleTrackDiscoveryService : ITrackDiscoveryService
{
    public Track LoadTrack(IFileInfo fileInfo)
    {
        var duration = GetTrackDuration(fileInfo);
        return LoadTrackWithDuration(fileInfo, duration);
    }

    public Track LoadTrackWithDuration(IFileInfo fileInfo, TimeSpan duration)
    {
        var (dance, artist, title) = ParseFileName(fileInfo);
        var format = ParseAudioFormat(fileInfo.Extension);
        return new Track(dance, artist, title, fileInfo, duration, format);
    }

    private static (string Dance, string Artist, string Title) ParseFileName(IFileInfo fileInfo)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var parts = nameWithoutExtension.Split(" - ", 3);

        return parts.Length != 3
            ? throw new FormatException(
                $"Invalid filename format for '{fileInfo.Name}', expected '{{Dance}} - {{Artist}} - {{Title}}.ext'.")
            : (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
    }

    private static AudioFormat ParseAudioFormat(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".mp3" or ".mp2" or ".mp1" => AudioFormat.Mp3,
            ".wav" => AudioFormat.Wav,
            ".flac" or ".fla" => AudioFormat.Flac,
            ".ogg" or ".oga" => AudioFormat.Ogg,
            ".aif" or ".aiff" => AudioFormat.Aif,
            _ => throw new ArgumentOutOfRangeException(nameof(ext), ext, $"Unsupported audio format for '{ext}'.")
        };
    }

    private static TimeSpan GetTrackDuration(IFileInfo fileInfo)
    {
        try
        {
            using var file = TagLib.File.Create(fileInfo.FullName);
            return file.Properties.Duration;
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"Unable to load track duration for '{fileInfo.Name}'.", exception);
        }
    }
}
