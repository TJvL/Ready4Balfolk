using Ready4Balfolk.Domain.Services.Discovery;

namespace Ready4Balfolk.Domain.Services.Tracks;

public interface ITrackDiscoveryService
{
    /// <summary>
    /// Reads everything one file has to say about itself, in a single open.
    /// </summary>
    /// <param name="fileInfo">The audio file.</param>
    /// <param name="musicRoot">
    /// The music directory, so the folders in between can be read as artist and album.
    /// </param>
    TrackEvidence Gather(FileInfo fileInfo, DirectoryInfo musicRoot);
}
