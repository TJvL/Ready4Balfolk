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
    ApplicationLanguage ApplicationLanguage,
    WindowState MainWindowState,
    IEnumerable<WindowState> PresentationWindowStates,
    IEnumerable<string> CollapsedBranches,
    // Last, and with a default, so settings files written before this existed still deserialize.
    bool ShowButtonText = false,
    bool QueueCutoffEnabled = false,
    // Minutes since midnight rather than a TimeSpan: a constructor default has to be a compile-time
    // constant, and 1380 is 23:00.
    int QueueCutoffMinutesOfDay = 1380,
    // How far past the cutoff the queue may still run before adds are refused.
    int QueueCutoffGraceMinutes = 2)
{
    public ApplicationSettings() : this(string.Empty, 6, 30, 0, true, false, true, ApplicationTheme.Automatic,
        ApplicationLanguage.English, new WindowState(), [], [])
    {
    }

    /// <summary>Time of day after which the queue stops accepting entries, clamped to a real time.</summary>
    public TimeSpan QueueCutoff => TimeSpan.FromMinutes(Math.Clamp(QueueCutoffMinutesOfDay, 0, (24 * 60) - 1));

    /// <summary>How far past the cutoff the queue may still run before adds are refused.</summary>
    public TimeSpan QueueCutoffGrace => TimeSpan.FromMinutes(Math.Max(0, QueueCutoffGraceMinutes));
}
