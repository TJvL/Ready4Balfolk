using System.IO.Abstractions;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery;

public interface IDanceFileDiscoveryService
{
    Dictionary<string, string> Matches(IDirectoryInfo directoryInfo);
}
