using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Services.Tracks;

public interface ITrackDiscoveryService
{
    Models.Tracks.Track LoadTrack(IFileInfo fileInfo);

    Models.Tracks.Track LoadTrackWithDuration(IFileInfo fileInfo, TimeSpan duration);
}
