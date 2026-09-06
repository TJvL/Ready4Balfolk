using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Presentation;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.UI.Views.Settings;

public sealed partial class SettingsViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILoggerService _loggerService;
    private readonly IConfirmationService _confirmationService;
    private readonly PresentationWebServer _webServer;
    private readonly IFileSystem _fileSystem;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>What ending a language change does. Replaced only by tests.</summary>
    private readonly Action _restart = RestartApplication;

    private bool _syncing;

    [Reactive] public partial string MusicDirectoryPath { get; set; }
    [Reactive] public partial int MaxQueueItems { get; set; }
    [Reactive] public partial int DelaySeconds { get; set; }
    [Reactive] public partial int PresentationDisplayCount { get; set; }
    [Reactive] public partial bool AutoQueueRandomTrack { get; set; }
    [Reactive] public partial bool AllowDuplicateTracksInQueue { get; set; }
    [Reactive] public partial bool RequirePlaybackConfirmation { get; set; }
    [Reactive] public partial bool ShowButtonText { get; set; }

    // A moment between one dance and the next, so a floor can clear without the DJ queueing a delay
    // every time.
    [Reactive] public partial bool GapBetweenTracksEnabled { get; set; }
    [Reactive] public partial int GapBetweenTracksSeconds { get; set; }

    // How a track is written on each screen that writes one as a line, in the user's own words.
    [Reactive] public partial string NowPlayingPrimaryTemplate { get; set; }
    [Reactive] public partial string NowPlayingSecondaryTemplate { get; set; }
    [Reactive] public partial string QueueItemTemplate { get; set; }
    [Reactive] public partial string HistoryItemTemplate { get; set; }

    /// <summary>What those four do to a track, so nobody has to guess before they save.</summary>
    [Reactive] public partial string TemplatePreview { get; private set; }
    [Reactive] public partial bool QueueCutoffEnabled { get; set; }
    [Reactive] public partial int QueueCutoffMinutesOfDay { get; set; }
    [Reactive] public partial int QueueCutoffGraceMinutes { get; set; }
    [Reactive] public partial string EndOfNightAudioPath { get; set; }
    [Reactive] public partial bool PlayEndOfNightAtCutoff { get; set; }

    /// <summary>
    /// True when a path has been typed or picked and there is nothing there, so the queue's button
    /// staying switched off is explained here rather than left a mystery.
    /// </summary>
    [Reactive] public partial bool IsEndOfNightAudioMissing { get; set; }

    [Reactive] public partial bool WebServerEnabled { get; set; }
    [Reactive] public partial int WebServerPort { get; set; }
    [Reactive] public partial bool WebRemoteControlEnabled { get; set; }
    [Reactive] public partial string WebRemoteControlPin { get; set; }

    /// <summary>What the server is actually doing, which is not the same as what the switch says.</summary>
    [Reactive] public partial string WebServerStatus { get; set; }

    /// <summary>The addresses to type into the other device, one per line.</summary>
    [Reactive] public partial string WebServerAddresses { get; set; }

    /// <summary>
    /// True while the socket is being bound or drained. Both take long enough to see, so the
    /// controls go quiet rather than letting a second click queue another whole cycle.
    /// </summary>
    [Reactive] public partial bool IsWebServerBusy { get; set; }

    [Reactive] public partial ApplicationTheme SelectedTheme { get; set; }
    [Reactive] public partial ApplicationLanguage SelectedLanguage { get; set; }

    public IReadOnlyList<ApplicationTheme> AvailableThemes { get; } =
        Enum.GetValues<ApplicationTheme>();

    public IReadOnlyList<ApplicationLanguage> AvailableLanguages { get; } =
        Enum.GetValues<ApplicationLanguage>();

    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var info = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return info is null || info.Contains("-dev") ? "dev" : info;
    }

    /// <summary>The same panel with the restart replaced, for tests.</summary>
    /// <remarks>
    /// The real one ends in <see cref="Environment.Exit(int)"/>, which would take the test host
    /// with it, so the accepted half of a language change is otherwise unreachable.
    /// </remarks>
    public SettingsViewModel(ISettingsStore settingsStore, ILoggerService loggerService,
        IConfirmationService confirmationService, PresentationWebServer webServer,
        IFileSystem fileSystem, Action restart)
        : this(settingsStore, loggerService, confirmationService, webServer, fileSystem)
    {
        _restart = restart;
    }

    public SettingsViewModel(ISettingsStore settingsStore, ILoggerService loggerService,
        IConfirmationService confirmationService, PresentationWebServer webServer,
        IFileSystem fileSystem)
    {
        _settingsStore = settingsStore;
        _loggerService = loggerService;
        _confirmationService = confirmationService;
        _webServer = webServer;
        _fileSystem = fileSystem;

        var current = settingsStore.Current;
        MusicDirectoryPath = current.MusicDirectoryPath;
        MaxQueueItems = current.MaxQueueItems;
        DelaySeconds = current.DelaySeconds;
        PresentationDisplayCount = current.PresentationDisplayCount;
        AutoQueueRandomTrack = current.AutoQueueRandomTrack;
        AllowDuplicateTracksInQueue = current.AllowDuplicateTracksInQueue;
        RequirePlaybackConfirmation = current.RequirePlaybackConfirmation;
        ShowButtonText = current.ShowButtonText;
        GapBetweenTracksEnabled = current.GapBetweenTracksEnabled;
        GapBetweenTracksSeconds = current.GapBetweenTracksSeconds;
        NowPlayingPrimaryTemplate = current.DisplayTemplates.NowPlayingPrimary;
        NowPlayingSecondaryTemplate = current.DisplayTemplates.NowPlayingSecondary;
        QueueItemTemplate = current.DisplayTemplates.QueueItem;
        HistoryItemTemplate = current.DisplayTemplates.HistoryItem;
        QueueCutoffEnabled = current.QueueCutoffEnabled;
        QueueCutoffMinutesOfDay = current.QueueCutoffMinutesOfDay;
        QueueCutoffGraceMinutes = current.QueueCutoffGraceMinutes;
        EndOfNightAudioPath = current.EndOfNightAudioPath;
        PlayEndOfNightAtCutoff = current.PlayEndOfNightAtCutoff;
        IsEndOfNightAudioMissing = false;
        WebServerEnabled = current.WebServerEnabled;
        WebServerPort = current.WebServerPort;
        WebRemoteControlEnabled = current.WebRemoteControlEnabled;
        WebRemoteControlPin = current.WebRemoteControlPin;
        WebServerStatus = "";
        WebServerAddresses = "";
        IsWebServerBusy = false;
        SelectedTheme = current.ApplicationTheme;
        SelectedLanguage = current.ApplicationLanguage;

        ThrottledSave(x => x.MusicDirectoryPath, v => s => s with
        {
            MusicDirectoryPath = v
        });
        ThrottledSave(x => x.MaxQueueItems, v => s => s with
        {
            MaxQueueItems = v
        });
        ThrottledSave(x => x.DelaySeconds, v => s => s with
        {
            DelaySeconds = v
        });
        ThrottledSave(x => x.PresentationDisplayCount, v => s => s with
        {
            PresentationDisplayCount = v
        });
        ThrottledSave(x => x.AutoQueueRandomTrack, v => s => s with
        {
            AutoQueueRandomTrack = v
        });
        ThrottledSave(x => x.AllowDuplicateTracksInQueue, v => s => s with
        {
            AllowDuplicateTracksInQueue = v
        });
        ThrottledSave(x => x.RequirePlaybackConfirmation, v => s => s with
        {
            RequirePlaybackConfirmation = v
        });
        ThrottledSave(x => x.ShowButtonText, v => s => s with
        {
            ShowButtonText = v
        });
        ThrottledSave(x => x.GapBetweenTracksEnabled, v => s => s with
        {
            GapBetweenTracksEnabled = v
        });
        ThrottledSave(x => x.GapBetweenTracksSeconds, v => s => s with
        {
            GapBetweenTracksSeconds = v
        });
        ThrottledSave(x => x.NowPlayingPrimaryTemplate, v => s => s with
        {
            DisplayTemplatesOrNull = s.DisplayTemplates with { NowPlayingPrimary = v }
        });
        ThrottledSave(x => x.NowPlayingSecondaryTemplate, v => s => s with
        {
            DisplayTemplatesOrNull = s.DisplayTemplates with { NowPlayingSecondary = v }
        });
        ThrottledSave(x => x.QueueItemTemplate, v => s => s with
        {
            DisplayTemplatesOrNull = s.DisplayTemplates with { QueueItem = v }
        });
        ThrottledSave(x => x.HistoryItemTemplate, v => s => s with
        {
            DisplayTemplatesOrNull = s.DisplayTemplates with { HistoryItem = v }
        });
        ThrottledSave(x => x.QueueCutoffEnabled, v => s => s with
        {
            QueueCutoffEnabled = v
        });
        ThrottledSave(x => x.QueueCutoffMinutesOfDay, v => s => s with
        {
            QueueCutoffMinutesOfDay = v
        });
        ThrottledSave(x => x.QueueCutoffGraceMinutes, v => s => s with
        {
            QueueCutoffGraceMinutes = v
        });
        ThrottledSave(x => x.EndOfNightAudioPath, v => s => s with
        {
            EndOfNightAudioPath = v
        });
        ThrottledSave(x => x.PlayEndOfNightAtCutoff, v => s => s with
        {
            PlayEndOfNightAtCutoff = v
        });
        ThrottledSave(x => x.WebServerEnabled, v => s => s with
        {
            WebServerEnabled = v
        });
        ThrottledSave(x => x.WebServerPort, v => s => s with
        {
            WebServerPort = v
        });
        // Switching the remote on for the first time mints its PIN, so there is never a moment
        // where the remote is reachable and the PIN is empty.
        ThrottledSave(x => x.WebRemoteControlEnabled, v => s => s with
        {
            WebRemoteControlEnabled = v,
            WebRemoteControlPin = v && s.WebRemoteControlPin.Length == 0
                ? RemoteAccessService.GeneratePin()
                : s.WebRemoteControlPin
        });
        ThrottledSave(x => x.SelectedTheme, v => s => s with
        {
            ApplicationTheme = v
        });

        // Checked here as well as at the queue's button, so a path that resolves to nothing is
        // answered where it was typed.
        this.WhenAnyValue(x => x.EndOfNightAudioPath)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Select(path => path.Length > 0 && !fileSystem.File.Exists(path))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(missing => IsEndOfNightAudioMissing = missing)
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedLanguage)
            .Skip(1)
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(language => OnLanguageChangedAsync(language).SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to change language", exception)))
            .DisposeWith(_disposables);

        settingsStore.Observe()
            .Skip(1)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(SyncFromStore)
            .DisposeWith(_disposables);

        // The preview runs as a template is typed, the way the pattern preview does on the
        // discovery screen: what a template does to a track is the only way to judge it.
        this.WhenAnyValue(
                x => x.NowPlayingPrimaryTemplate,
                x => x.NowPlayingSecondaryTemplate,
                x => x.QueueItemTemplate,
                x => x.HistoryItemTemplate)
            .Subscribe(_ => UpdatePreview())
            .DisposeWith(_disposables);

        UpdateWebServerStatus();
        webServer.WhenChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateWebServerStatus())
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// What the four templates make of one track, on the four lines they are for.
    /// </summary>
    /// <remarks>
    /// A track with everything on it, because a preview of the happy case is what somebody is
    /// writing the template against. What a missing field does is written beside the boxes rather
    /// than demonstrated, or the preview would have to be four more lines.
    /// </remarks>
    private void UpdatePreview()
    {
        var sample = new Track(
            UiStrings.Settings_TemplateSampleDance,
            UiStrings.Settings_TemplateSampleArtist,
            UiStrings.Settings_TemplateSampleTitle,
            _fileSystem.FileInfo.New("sample.mp3"),
            TimeSpan.FromMinutes(3),
            AudioFormat.Mp3);

        TemplatePreview = string.Join(
            Environment.NewLine,
            TrackTextTemplate.Render(NowPlayingPrimaryTemplate, sample),
            TrackTextTemplate.Render(NowPlayingSecondaryTemplate, sample),
            TrackTextTemplate.Render(QueueItemTemplate, sample),
            TrackTextTemplate.Render(HistoryItemTemplate, sample));
    }

    /// <summary>Mints a new PIN, which also drops every phone currently connected.</summary>
    [ReactiveCommand]
    private void RegeneratePin()
    {
        var pin = RemoteAccessService.GeneratePin();
        WebRemoteControlPin = pin;
        CommitDirectAsync(s => s with { WebRemoteControlPin = pin }).SafeFireAndForget(exception =>
            _loggerService.ErrorAsync("Failed to save the remote control pin", exception));
    }

    private void UpdateWebServerStatus()
    {
        var state = _webServer.State;

        IsWebServerBusy = state is WebServerState.Starting or WebServerState.Stopping;

        WebServerStatus = state switch
        {
            WebServerState.Starting => UiStrings.Settings_WebServerStarting,
            WebServerState.Stopping => UiStrings.Settings_WebServerStopping,
            WebServerState.Running => UiStrings.Settings_WebServerRunning,
            WebServerState.Failed => string.Format(
                CultureInfo.CurrentCulture,
                UiStrings.Settings_WebServerFailed,
                _webServer.LastError ?? ""),
            // Stopped with a reason is a server that was switched off because it could not run.
            // Without this the box unticks itself and says nothing about why.
            _ when _webServer.LastError is { Length: > 0 } reason => string.Format(
                CultureInfo.CurrentCulture, UiStrings.Settings_WebServerSwitchedOff, reason),
            _ => UiStrings.Settings_WebServerStopped
        };

        WebServerAddresses = state is WebServerState.Running
            ? string.Join(Environment.NewLine, _webServer.Addresses)
            : "";
    }

    private void SyncFromStore(ApplicationSettings s)
    {
        _syncing = true;
        MusicDirectoryPath = s.MusicDirectoryPath;
        MaxQueueItems = s.MaxQueueItems;
        DelaySeconds = s.DelaySeconds;
        PresentationDisplayCount = s.PresentationDisplayCount;
        AutoQueueRandomTrack = s.AutoQueueRandomTrack;
        AllowDuplicateTracksInQueue = s.AllowDuplicateTracksInQueue;
        RequirePlaybackConfirmation = s.RequirePlaybackConfirmation;
        ShowButtonText = s.ShowButtonText;
        GapBetweenTracksEnabled = s.GapBetweenTracksEnabled;
        GapBetweenTracksSeconds = s.GapBetweenTracksSeconds;
        QueueCutoffEnabled = s.QueueCutoffEnabled;
        QueueCutoffMinutesOfDay = s.QueueCutoffMinutesOfDay;
        QueueCutoffGraceMinutes = s.QueueCutoffGraceMinutes;
        EndOfNightAudioPath = s.EndOfNightAudioPath;
        PlayEndOfNightAtCutoff = s.PlayEndOfNightAtCutoff;
        WebServerEnabled = s.WebServerEnabled;
        WebServerPort = s.WebServerPort;
        WebRemoteControlEnabled = s.WebRemoteControlEnabled;
        WebRemoteControlPin = s.WebRemoteControlPin;
        SelectedTheme = s.ApplicationTheme;
        SelectedLanguage = s.ApplicationLanguage;
        _syncing = false;
    }

    private void ThrottledSave<T>(
        System.Linq.Expressions.Expression<Func<SettingsViewModel, T>> property,
        Func<T, Func<ApplicationSettings, ApplicationSettings>> transform)
    {
        this.WhenAnyValue(property)
            .Skip(1)
            // Asked here rather than at the write, which happens 300ms later, by which time
            // SyncFromStore has long since put the flag back down and a change that arrived from
            // the store is written straight back out.
            .Where(_ => !_syncing)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(value => CommitDirectAsync(transform(value)).SafeFireAndForget(exception =>
                _loggerService.ErrorAsync("Failed to save settings", exception)))
            .DisposeWith(_disposables);
    }

    /// <summary>Writes one settings change out.</summary>
    /// <remarks>
    /// A Task rather than async void, so a failure has somewhere to go. As async void it was thrown
    /// at the process-level handler, which is the last place a settings write should surface.
    /// Callers are Rx subscriptions, so they hand it to SafeFireAndForget.
    /// </remarks>
    private async Task CommitDirectAsync(Func<ApplicationSettings, ApplicationSettings> transform)
    {
        if (_syncing)
        {
            return;
        }

        try
        {
            await _settingsStore.UpdateAsync(transform);
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to save settings", ex);
        }
    }

    public async Task ExportLogAsync(string path) => await _loggerService.ExportAsync(path);

    private async Task OnLanguageChangedAsync(ApplicationLanguage newLanguage)
    {
        if (_syncing)
        {
            return;
        }

        var currentLanguage = _settingsStore.Current.ApplicationLanguage;
        if (newLanguage == currentLanguage)
        {
            return;
        }

        try
        {
            var confirmed = await _confirmationService.ConfirmAsync(
                UiStrings.Settings_LanguageRestartTitle,
                UiStrings.Settings_LanguageRestartMessage,
                UiStrings.Dialog_Restart,
                UiStrings.Dialog_Cancel,
                // Restarting tears the application down, which is not something to walk into from a
                // dropdown that was opened by accident.
                ConfirmationStakes.Destructive);

            if (!confirmed)
            {
                _syncing = true;
                SelectedLanguage = currentLanguage;
                _syncing = false;
                return;
            }

            await _settingsStore.UpdateAsync(s => s with
            {
                ApplicationLanguage = newLanguage
            });
            _restart();
        }
        catch (Exception ex)
        {
            _ = _loggerService.ErrorAsync("Failed to change language", ex);
        }
    }

    private static void RestartApplication()
    {
        var exePath = Environment.ProcessPath;
        if (exePath is not null)
        {
            // Use setsid to start in a new session, fully detached from parent
            Process.Start(new ProcessStartInfo
            {
                FileName = "setsid",
                Arguments = $"--fork /bin/sh -c \"sleep 0.3 && '{exePath}'\"",
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = false
            });
        }

        Environment.Exit(0);
    }

    public void Dispose() => _disposables.Dispose();
}
