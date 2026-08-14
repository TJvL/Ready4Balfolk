using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
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
    private readonly CompositeDisposable _disposables = [];
    private bool _syncing;

    [Reactive] public partial string MusicDirectoryPath { get; set; }
    [Reactive] public partial int MaxQueueItems { get; set; }
    [Reactive] public partial int DelaySeconds { get; set; }
    [Reactive] public partial int PresentationDisplayCount { get; set; }
    [Reactive] public partial bool AutoQueueRandomTrack { get; set; }
    [Reactive] public partial bool AllowDuplicateTracksInQueue { get; set; }
    [Reactive] public partial bool RequirePlaybackConfirmation { get; set; }
    [Reactive] public partial bool ShowButtonText { get; set; }
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

    public SettingsViewModel(ISettingsStore settingsStore, ILoggerService loggerService,
        IConfirmationService confirmationService, PresentationWebServer webServer)
    {
        _settingsStore = settingsStore;
        _loggerService = loggerService;
        _confirmationService = confirmationService;
        _webServer = webServer;

        var current = settingsStore.Current;
        MusicDirectoryPath = current.MusicDirectoryPath;
        MaxQueueItems = current.MaxQueueItems;
        DelaySeconds = current.DelaySeconds;
        PresentationDisplayCount = current.PresentationDisplayCount;
        AutoQueueRandomTrack = current.AutoQueueRandomTrack;
        AllowDuplicateTracksInQueue = current.AllowDuplicateTracksInQueue;
        RequirePlaybackConfirmation = current.RequirePlaybackConfirmation;
        ShowButtonText = current.ShowButtonText;
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
            .Select(path => path.Length > 0 && !File.Exists(path))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(missing => IsEndOfNightAudioMissing = missing)
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedLanguage)
            .Skip(1)
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(OnLanguageChanged)
            .DisposeWith(_disposables);

        settingsStore.Observe()
            .Skip(1)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(SyncFromStore)
            .DisposeWith(_disposables);

        UpdateWebServerStatus();
        webServer.WhenChanged
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => UpdateWebServerStatus())
            .DisposeWith(_disposables);
    }

    /// <summary>Mints a new PIN, which also drops every phone currently connected.</summary>
    [ReactiveCommand]
    private void RegeneratePin()
    {
        var pin = RemoteAccessService.GeneratePin();
        WebRemoteControlPin = pin;
        CommitDirect(s => s with { WebRemoteControlPin = pin });
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
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(value => CommitDirect(transform(value)))
            .DisposeWith(_disposables);
    }

    private async void CommitDirect(Func<ApplicationSettings, ApplicationSettings> transform)
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

    public async Task ExportLogAsync(FileInfo file) => await _loggerService.ExportAsync(file);

    private async void OnLanguageChanged(ApplicationLanguage newLanguage)
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
                UiStrings.Dialog_Cancel);

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
            RestartApplication();
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
