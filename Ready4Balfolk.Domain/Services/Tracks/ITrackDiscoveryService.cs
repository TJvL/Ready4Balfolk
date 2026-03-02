namespace Ready4Balfolk.Domain.Services.Tracks;

public interface ITrackDiscoveryService
{
    Models.Tracks.Track LoadTrack(FileInfo fileInfo);

    Models.Tracks.Track LoadTrackWithDuration(FileInfo fileInfo, TimeSpan duration);
}
