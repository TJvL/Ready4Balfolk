using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>Everything the application remembers between runs, as it is written to settings.json.</summary>
/// <remarks>
/// Every stored member, here and in the records it is made of, names itself with
/// <c>[JsonPropertyName]</c> in the spelling it has always been written under. The file is one a DJ
/// is invited to edit by hand, and it is read skipping whatever it does not recognise, so a property
/// renamed without its stored name would put that setting back to its default without a word. A
/// test holds the names, so renaming one on disk is a decision rather than an accident.
/// </remarks>
public sealed record ApplicationSettings(
    [property: JsonPropertyName("MusicDirectoryPath")] string MusicDirectoryPath,
    [property: JsonPropertyName("MaxQueueItems")] int MaxQueueItems,
    [property: JsonPropertyName("DelaySeconds")] int DelaySeconds,
    [property: JsonPropertyName("PresentationDisplayCount")] int PresentationDisplayCount,
    [property: JsonPropertyName("AutoQueueRandomTrack")] bool AutoQueueRandomTrack,
    [property: JsonPropertyName("AllowDuplicateTracksInQueue")] bool AllowDuplicateTracksInQueue,
    [property: JsonPropertyName("RequirePlaybackConfirmation")] bool RequirePlaybackConfirmation,
    [property: JsonPropertyName("ApplicationTheme")] ApplicationTheme ApplicationTheme,
    [property: JsonPropertyName("ApplicationLanguage")] ApplicationLanguage ApplicationLanguage,
    [property: JsonPropertyName("MainWindowState")] WindowState MainWindowState,
    [property: JsonPropertyName("PresentationWindowStates")] IEnumerable<WindowState> PresentationWindowStates,
    // Last, and with a default, so settings files written before this existed still deserialize.
    [property: JsonPropertyName("ShowButtonText")] bool ShowButtonText = false,
    [property: JsonPropertyName("QueueCutoffEnabled")] bool QueueCutoffEnabled = false,
    // Minutes since midnight rather than a TimeSpan: a constructor default has to be a compile-time
    // constant, and 1380 is 23:00.
    [property: JsonPropertyName("QueueCutoffMinutesOfDay")] int QueueCutoffMinutesOfDay = 1380,
    // How far past the cutoff the queue may still run before adds are refused.
    [property: JsonPropertyName("QueueCutoffGraceMinutes")] int QueueCutoffGraceMinutes = 2,
    // Null rather than a flat instance, because a constructor default has to be a compile-time
    // constant. Read it through Equalizer, never directly.
    [property: JsonPropertyName("EqualizerOrNull")] EqualizerSettings? EqualizerOrNull = null,
    // Serve the presentation display and the remote to a browser. Off by default: the app works
    // without it, and a listening socket nobody asked for is not something to switch on for them.
    [property: JsonPropertyName("WebServerEnabled")] bool WebServerEnabled = false,
    [property: JsonPropertyName("WebServerPort")] int WebServerPort = 8420,
    // A second switch, because the display page is harmless and the remote is not: anyone who can
    // reach it can skip the track a hall full of people is dancing to.
    [property: JsonPropertyName("WebRemoteControlEnabled")] bool WebRemoteControlEnabled = false,
    // Empty until the remote is first enabled, at which point one is generated.
    [property: JsonPropertyName("WebRemoteControlPin")] string WebRemoteControlPin = "",
    // False on a settings file written before the wizard existed, which is the right answer: those
    // profiles have no dance list either, so they get the same first run as a new one.
    [property: JsonPropertyName("SetupCompleted")] bool SetupCompleted = false,
    // Null rather than an instance, for the same reason as the equalizer: a constructor default has
    // to be a compile-time constant. Read it through Discovery, never directly.
    [property: JsonPropertyName("DiscoveryOrNull")] DiscoverySettings? DiscoveryOrNull = null,
    // Off, because the shared list is what makes a dance name mean the same thing to everybody, and
    // a local answer is a proposal at BigBalfolkList waiting to be made. On, a track you have
    // answered reaches the library whatever you called the dance, at the price that a random pick
    // draws by tag, and a dance the list has never heard of carries none.
    [property: JsonPropertyName("AllowDancesOutsideTheList")] bool AllowDancesOutsideTheList = false,
    // One file that lives wherever the user keeps it: no library, no import, no copy. Empty until
    // somebody says what the sound of the evening ending is, and until then there is nothing to
    // offer to play.
    [property: JsonPropertyName("EndOfNightAudioPath")] string EndOfNightAudioPath = "",
    // Play it after the last track the cutoff allowed, so nobody has to remember to press anything
    // while packing up.
    [property: JsonPropertyName("PlayEndOfNightAtCutoff")] bool PlayEndOfNightAtCutoff = false,
    // Null rather than an instance, for the same reason as the equalizer: a constructor default has
    // to be a compile-time constant. Read it through DisplayTemplates, never directly.
    [property: JsonPropertyName("DisplayTemplatesOrNull")] DisplayTemplates? DisplayTemplatesOrNull = null,
    // A moment between one dance and the next, so a floor can clear and re-form without the DJ
    // queueing a delay every time. Off by default: an evening that wants no gaps should get none.
    [property: JsonPropertyName("GapBetweenTracksEnabled")] bool GapBetweenTracksEnabled = false,
    [property: JsonPropertyName("GapBetweenTracksSeconds")] int GapBetweenTracksSeconds = 10)
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

    /// <summary>What the user has declared about the shape of their library, empty until they do.</summary>
    [JsonIgnore]
    public DiscoverySettings Discovery => DiscoveryOrNull ?? DiscoverySettings.Undeclared;

    /// <summary>Output equalizer, flat when the settings file predates it.</summary>
    /// <remarks>
    /// Ignored for serialization, or the whole equalizer would be written twice, once here and
    /// once under EqualizerOrNull, with only the latter ever read back.
    /// </remarks>
    [JsonIgnore]
    public EqualizerSettings Equalizer => EqualizerOrNull ?? EqualizerSettings.Flat;

    /// <summary>How tracks are written on the screens that write them as a line.</summary>
    [JsonIgnore]
    public DisplayTemplates DisplayTemplates => DisplayTemplatesOrNull ?? DisplayTemplates.Default;

    /// <summary>The quiet between one dance and the next, or nothing when it is switched off.</summary>
    /// <remarks>Clamped to something a room would recognise as a gap rather than as a fault.</remarks>
    [JsonIgnore]
    public TimeSpan GapBetweenTracks => GapBetweenTracksEnabled
        ? TimeSpan.FromSeconds(Math.Clamp(GapBetweenTracksSeconds, 1, 120))
        : TimeSpan.Zero;
}
