using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Models.Settings;

public sealed record ApplicationSettings(
    string MusicDirectoryPath,
    int MaxQueueItems,
    int DelaySeconds,
    int PresentationDisplayCount,
    bool AutoQueueRandomTrack,
    bool AllowDuplicateTracksInQueue,
    bool RequirePlaybackConfirmation,
    bool AdditionalSongInformationRetrieval,
    DiscoveryPattern DiscoveryPattern,
    ApplicationTheme ApplicationTheme,
    ApplicationLanguage ApplicationLanguage,
    WindowState MainWindowState,
    IEnumerable<WindowState> PresentationWindowStates,
    IEnumerable<string> CollapsedBranches)
{
    public ApplicationSettings() : this(string.Empty, 6, 30, 0, true, false, true, true, Tracks.DiscoveryPattern.DefaultDefault, ApplicationTheme.Automatic,
        ApplicationLanguage.English, new WindowState(), [], [])
    {
    }
}
