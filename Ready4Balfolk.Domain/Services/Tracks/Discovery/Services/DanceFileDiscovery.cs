using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Domain.Services.Tracks.Discovery.Services;

public class DanceFileDiscovery(IDanceFileDiscoveryService danceFileDiscovery, ISettingsStore settingsStore)
    : IPatternSegmentDiscovery, IDiscoveryOrder
{
    public int Order => 2;

    public IEnumerable<KeyValuePair<PatternSegment, string>> Scan(IFileInfo fileInfo)
    {
        // dances.json discovery also writes template files into the music
        // directories, so it is only active when the user opted in.
        if (!settingsStore.Current.AdditionalSongInformationRetrieval)
        {
            yield break;
        }

        if (fileInfo.Directory == null)
        {
            yield break;
        }

        var fileMatches = danceFileDiscovery.Matches(fileInfo.Directory);
        if (!fileMatches.TryGetValue(fileInfo.Name, out var dance))
        {
            yield break;
        }

        yield return new KeyValuePair<PatternSegment, string>(PatternSegment.Dance, dance);
    }

}
