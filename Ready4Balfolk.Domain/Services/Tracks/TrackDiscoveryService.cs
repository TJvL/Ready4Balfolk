using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class TrackDiscoveryService : ITrackDiscoveryService
{
    public Track LoadTrack(FileInfo fileInfo)
    {
        var (dance, artist, title) = ParseFileName(fileInfo);
        var duration = GetTrackDuration(fileInfo);
        var format = ParseAudioFormat(fileInfo);
        return new Track(dance, artist, title, fileInfo, duration, format);
    }

    public Track LoadTrackWithDuration(FileInfo fileInfo, TimeSpan duration)
    {
        var (dance, artist, title) = ParseFileName(fileInfo);
        var format = ParseAudioFormat(fileInfo);
        return new Track(dance, artist, title, fileInfo, duration, format);
    }

    private static (string Dance, string Artist, string Title) ParseFileName(FileInfo fileInfo)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
        var parts = nameWithoutExtension.Split(" - ", 3);

        return parts.Length != 3
            ? throw new FormatException(
                $"Invalid filename format for '{fileInfo.Name}', expected '{{Dance}} - {{Artist}} - {{Title}}.ext'.")
            : (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
    }

    private static AudioFormat ParseAudioFormat(FileInfo fileInfo)
    {
        var ext = fileInfo.Extension;
        return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mp2", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mp1", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Mp3
            : ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Wav
            : ext.Equals(".flac", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fla", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Flac
            : ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".oga", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Ogg
            : ext.Equals(".aif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aiff", StringComparison.OrdinalIgnoreCase)
            ? AudioFormat.Aif
            : throw new ArgumentOutOfRangeException(nameof(fileInfo), ext,
                $"Unsupported audio format for '{fileInfo.Name}'.");
    }

    private static TimeSpan GetTrackDuration(FileInfo fileInfo)
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
