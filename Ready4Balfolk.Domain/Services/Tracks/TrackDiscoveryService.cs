using Ready4Balfolk.Domain.Models.Tracks;
using TagLibSharp2.Mpeg;

namespace Ready4Balfolk.Domain.Services.Tracks;

public sealed class TrackDiscoveryService : ITrackDiscoveryService
{
    public async Task<Track> LoadTrackAsync(FileInfo mp3FileInfo)
    {
        var (dance, artist, title) = ParseFileName(mp3FileInfo);
        var duration = await GetTrackDurationAsync(mp3FileInfo);
        return new Track(dance, artist, title, mp3FileInfo, duration);
    }

    private static (string Dance, string Artist, string Title) ParseFileName(FileInfo mp3FileInfo)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(mp3FileInfo.Name);
        var parts = nameWithoutExtension.Split(" - ", 3);

        return parts.Length != 3
            ? throw new FormatException(
                $"Invalid filename format for '{mp3FileInfo.Name}', expected '{{Dance}} - {{Artist}} - {{Title}}.mp3'.")
            : ((string Dance, string Artist, string Title))(parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
    }

    private static async Task<TimeSpan> GetTrackDurationAsync(FileInfo mp3FileInfo)
    {
        try
        {
            using var mp3File = (await Mp3File.ReadFromFileAsync(mp3FileInfo.FullName)).File ?? throw new IOException($"Unable to read mp3 file '{mp3FileInfo.Name}'.");

            return mp3File.Duration ?? TimeSpan.Zero;
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"Unable to load track duration for '{mp3FileInfo.Name}'.", exception);
        }
    }
}
