using System.Text.Json.Serialization;

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
    // Last, and with a default, so settings files written before this existed still deserialize.
    bool ShowButtonText = false,
    bool QueueCutoffEnabled = false,
    // Minutes since midnight rather than a TimeSpan: a constructor default has to be a compile-time
    // constant, and 1380 is 23:00.
    int QueueCutoffMinutesOfDay = 1380,
    // How far past the cutoff the queue may still run before adds are refused.
    int QueueCutoffGraceMinutes = 2,
    // Null rather than a flat instance, because a constructor default has to be a compile-time
    // constant. Read it through Equalizer, never directly.
    EqualizerSettings? EqualizerOrNull = null,
    // Serve the presentation display and the remote to a browser. Off by default: the app works
    // without it, and a listening socket nobody asked for is not something to switch on for them.
    bool WebServerEnabled = false,
    int WebServerPort = 8420,
    // A second switch, because the display page is harmless and the remote is not: anyone who can
    // reach it can skip the track a hall full of people is dancing to.
    bool WebRemoteControlEnabled = false,
    // Empty until the remote is first enabled, at which point one is generated.
    string WebRemoteControlPin = "",
    // False on a settings file written before the wizard existed, which is the right answer: those
    // profiles have no dance list either, so they get the same first run as a new one.
    bool SetupCompleted = false)
{
    public ApplicationSettings() : this(string.Empty, 6, 30, 0, true, false, true, ApplicationTheme.Automatic,
        ApplicationLanguage.English, new WindowState(), [])
    {
    }

    /// <summary>Time of day after which the queue stops accepting entries, clamped to a real time.</summary>
    public TimeSpan QueueCutoff => TimeSpan.FromMinutes(Math.Clamp(QueueCutoffMinutesOfDay, 0, (24 * 60) - 1));

    /// <summary>How far past the cutoff the queue may still run before adds are refused.</summary>
    public TimeSpan QueueCutoffGrace => TimeSpan.FromMinutes(Math.Max(0, QueueCutoffGraceMinutes));

    /// <summary>The port the embedded server listens on, clamped to the unprivileged range.</summary>
    /// <remarks>Below 1024 needs root on Linux, which this app will never have.</remarks>
    [JsonIgnore]
    public int WebServerPortClamped => Math.Clamp(WebServerPort, 1024, 65535);

    /// <summary>Output equalizer, flat when the settings file predates it.</summary>
    /// <remarks>
    /// Ignored for serialization, or the whole equalizer would be written twice, once here and
    /// once under EqualizerOrNull, with only the latter ever read back.
    /// </remarks>
    [JsonIgnore]
    public EqualizerSettings Equalizer => EqualizerOrNull ?? EqualizerSettings.Flat;
}
