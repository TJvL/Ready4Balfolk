namespace Ready4Balfolk.Domain.Models.Settings;

public sealed record ApplicationSettings(
    string MusicDirectoryPath,
    int MaxQueueItems,
    int DelaySeconds,
    int PresentationDisplayCount,
    bool AutoQueueRandomTrack,
    bool AllowDuplicateTracksInQueue,
    bool RequirePlaybackConfirmation,
    ApplicationTheme ApplicationTheme,
    WindowState MainWindowState,
    IEnumerable<WindowState> PresentationWindowStates,
    IEnumerable<string> CollapsedBranches)
{
    public ApplicationSettings() : this(string.Empty, 6, 30, 0, true, false, true, ApplicationTheme.Automatic,
        new WindowState(), [], [])
    {
    }
}
