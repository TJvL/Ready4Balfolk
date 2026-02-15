namespace Ready4Balfolk.Domain.Services.Tracks;

public interface ITrackDiscoveryService
{
    Task<Models.Tracks.Track> LoadTrackAsync(FileInfo mp3FileInfo);
}
