using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Platform;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;
using Ready4Balfolk.UI.Views.DanceList;
using Ready4Balfolk.Web;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.UI.Views.Settings;

public sealed partial class SettingsViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly IDanceListStore _danceListStore;
    private readonly ILoggerService _loggerService;
    private readonly INotificationService _notifications;
    private readonly IConfirmationService _confirmationService;
    private readonly PresentationWebServer _webServer;
    private readonly IFileSystem _fileSystem;
    private readonly IReadOnlyList<SettingField> _fields;
    private readonly IScheduler _saveScheduler;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>What ending a language change does. Replaced only by tests.</summary>
    private readonly Action _restart = ApplicationRestart.Run;

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

    /// <summary>Where the dance list came from and when, so a stale one is visible rather than assumed.</summary>
    [Reactive] public partial string DanceListOriginText { get; private set; }

    [Reactive] public partial bool IsUpdatingDanceList { get; private set; }

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
    public SettingsViewModel(ISettingsStore settingsStore, IDanceListStore danceListStore, ILoggerService loggerService,
        INotificationService notifications, IConfirmationService confirmationService,
        PresentationWebServer webServer, IFileSystem fileSystem, Action restart,
        IScheduler? saveScheduler = null)
        : this(settingsStore, danceListStore, loggerService, notifications, confirmationService, webServer, fileSystem,
            saveScheduler)
    {
        _restart = restart;
    }

    /// <remarks>
    /// <c>saveScheduler</c> is where the three tenths of a second between the last change to a
    /// control and the write to disk are counted. Real time unless a caller says otherwise, and
    /// only a test does: sleeping past a real throttle is the failure that passes on a quiet
    /// machine and fails on a busy one.
    /// </remarks>
    public SettingsViewModel(ISettingsStore settingsStore, IDanceListStore danceListStore, ILoggerService loggerService,
        INotificationService notifications, IConfirmationService confirmationService,
        PresentationWebServer webServer, IFileSystem fileSystem, IScheduler? saveScheduler = null)
    {
        _saveScheduler = saveScheduler ?? DefaultScheduler.Instance;
        _settingsStore = settingsStore;
        _danceListStore = danceListStore;
        _loggerService = loggerService;
        _notifications = notifications;
        _confirmationService = confirmationService;
        _webServer = webServer;
        _fileSystem = fileSystem;

        IsEndOfNightAudioMissing = false;
        WebServerStatus = "";
        WebServerAddresses = "";
        IsWebServerBusy = false;
        DanceListOriginText = string.Empty;

        _fields = Fields();
        foreach (var field in _fields)
        {
            field.Show(settingsStore.Current);
            field.SaveWhenChanged();
        }

        // Checked here as well as at the queue's button, so a path that resolves to nothing is
        // answered where it was typed.
        this.WhenAnyValue(x => x.EndOfNightAudioPath)
            .Throttle(TimeSpan.FromMilliseconds(300), _saveScheduler)
            .Select(path => path.Length > 0 && !fileSystem.File.Exists(path))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(missing => IsEndOfNightAudioMissing = missing)
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.SelectedLanguage)
            .Skip(1)
            .DistinctUntilChanged()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(language => OnLanguageChangedAsync(language).SafeFireAndForget(exception =>
                _loggerService.Report(
                    "Failed to change language", _notifications, UiStrings.Settings_LanguageChangeFailed, exception)))
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

        danceListStore.ObserveStatus()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(status => DanceListOriginText = DanceListReports.Origin(status))
            .DisposeWith(_disposables);

        _disposables.Add(RegeneratePinCommand.ReportFailures(
            _loggerService, "Failed to make a new PIN", _notifications, UiStrings.Settings_NewPinFailed));
        _disposables.Add(UpdateDanceListCommand.ReportFailures(
            _loggerService, "Failed to update the dance list", _notifications, UiStrings.DanceList_UpdateCommandFailed));
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
            _loggerService.Report(
                "Failed to save the remote control pin", _notifications, UiStrings.Settings_PinSaveFailed, exception));
    }

    /// <summary>Asks BigBalfolkList for a newer dance list.</summary>
    /// <remarks>
    /// Here rather than on the dance panel, which is where it used to be: that panel is for choosing
    /// what random picks from, and nobody looked there for upkeep of the list itself.
    /// </remarks>
    [ReactiveCommand]
    private async Task UpdateDanceListAsync()
    {
        IsUpdatingDanceList = true;
        try
        {
            _notifications.Show(await _danceListStore.RefreshAsync(), _danceListStore.Status);
        }
        finally
        {
            IsUpdatingDanceList = false;
        }
    }

    /// <summary>Takes a dance list from a file, for a machine that never reaches the internet.</summary>
    /// <remarks>
    /// <para>
    /// Takes the path the file picker handed back rather than a file object, so the code-behind
    /// does not have to reach for a filesystem of its own to build one.
    /// </para>
    /// <para>
    /// A file the store throws on rather than refuses is reported once, in the words a refusal
    /// would have used, and written to the log in English beside it. It used to be said twice on
    /// screen, an English line through the log and the translated one beside it, for the one file,
    /// and the translated one carried the exception's own text in whatever language it was in.
    /// </para>
    /// </remarks>
    public async Task UpdateDanceListFromFileAsync(string sourcePath)
    {
        IsUpdatingDanceList = true;
        try
        {
            _notifications.Show(
                await _danceListStore.UpdateFromFileAsync(_fileSystem.FileInfo.New(sourcePath)),
                _danceListStore.Status);
        }
        catch (Exception exception)
        {
            _loggerService.Report(
                "Failed to update the dance list from a file",
                _notifications,
                DanceListReports.Failed(DomainStrings.DanceList_FileUnreadable, _danceListStore.Status),
                exception);
        }
        finally
        {
            IsUpdatingDanceList = false;
        }
    }

    private void UpdateWebServerStatus()
    {
        var state = _webServer.State;

        // Asked once. Every read of it enumerates the machine's adapters, and two reads a moment
        // apart can disagree, which is a line that says there is no address over a block printing
        // one.
        var addresses = _webServer.Addresses;

        IsWebServerBusy = state is WebServerState.Starting or WebServerState.Stopping;

        WebServerStatus = state switch
        {
            WebServerState.Starting => UiStrings.Settings_WebServerStarting,
            WebServerState.Stopping => UiStrings.Settings_WebServerStopping,
            // The pages are being served and no other device can get at them, so the one address
            // that does work is spelled out: without it the panel says "only here" and prints
            // nothing, leaving the port to be read off the spinner.
            WebServerState.Running when addresses.Count == 0 && _webServer.BoundPort is { } port =>
                string.Format(
                    CultureInfo.CurrentCulture,
                    UiStrings.Settings_WebServerRunningNoAddress,
                    $"http://localhost:{port}"),
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
            ? string.Join(Environment.NewLine, addresses)
            : "";
    }

    private void SyncFromStore(ApplicationSettings s)
    {
        _syncing = true;
        foreach (var field in _fields)
        {
            field.Show(s);
        }

        _syncing = false;
    }

    /// <summary>One setting the screen shows: how it is read, and how a change to it is written.</summary>
    /// <param name="Show">Puts the stored value on screen.</param>
    /// <param name="SaveWhenChanged">Writes a change made on screen, or nothing for a setting saved elsewhere.</param>
    private sealed record SettingField(Action<ApplicationSettings> Show, Action SaveWhenChanged);

    /// <summary>Every setting the screen shows, listed once.</summary>
    /// <remarks>
    /// Loading, saving and following the store all walk this one list. Written out three times, the
    /// three had already drifted: the display templates were loaded and saved but never followed,
    /// so a change made anywhere else left this screen showing the old one. The language and the
    /// PIN are shown and followed like the rest, and written by their own paths, because changing
    /// either asks something first.
    /// </remarks>
    private IReadOnlyList<SettingField> Fields() =>
    [
        Field(s => s.MusicDirectoryPath, v => MusicDirectoryPath = v),
        Field(s => s.MaxQueueItems, v => MaxQueueItems = v, x => x.MaxQueueItems, (s, v) => s with { MaxQueueItems = v }),
        Field(s => s.DelaySeconds, v => DelaySeconds = v, x => x.DelaySeconds, (s, v) => s with { DelaySeconds = v }),
        Field(s => s.PresentationDisplayCount, v => PresentationDisplayCount = v, x => x.PresentationDisplayCount,
            (s, v) => s with { PresentationDisplayCount = v }),
        Field(s => s.AutoQueueRandomTrack, v => AutoQueueRandomTrack = v, x => x.AutoQueueRandomTrack,
            (s, v) => s with { AutoQueueRandomTrack = v }),
        Field(s => s.AllowDuplicateTracksInQueue, v => AllowDuplicateTracksInQueue = v, x => x.AllowDuplicateTracksInQueue,
            (s, v) => s with { AllowDuplicateTracksInQueue = v }),
        Field(s => s.RequirePlaybackConfirmation, v => RequirePlaybackConfirmation = v, x => x.RequirePlaybackConfirmation,
            (s, v) => s with { RequirePlaybackConfirmation = v }),
        Field(s => s.ShowButtonText, v => ShowButtonText = v, x => x.ShowButtonText, (s, v) => s with { ShowButtonText = v }),
        Field(s => s.GapBetweenTracksEnabled, v => GapBetweenTracksEnabled = v, x => x.GapBetweenTracksEnabled,
            (s, v) => s with { GapBetweenTracksEnabled = v }),
        Field(s => s.GapBetweenTracksSeconds, v => GapBetweenTracksSeconds = v, x => x.GapBetweenTracksSeconds,
            (s, v) => s with { GapBetweenTracksSeconds = v }),
        Field(s => s.DisplayTemplates.NowPlayingPrimary, v => NowPlayingPrimaryTemplate = v, x => x.NowPlayingPrimaryTemplate,
            (s, v) => s with { DisplayTemplatesOrNull = s.DisplayTemplates with { NowPlayingPrimary = v } }),
        Field(s => s.DisplayTemplates.NowPlayingSecondary, v => NowPlayingSecondaryTemplate = v, x => x.NowPlayingSecondaryTemplate,
            (s, v) => s with { DisplayTemplatesOrNull = s.DisplayTemplates with { NowPlayingSecondary = v } }),
        Field(s => s.DisplayTemplates.QueueItem, v => QueueItemTemplate = v, x => x.QueueItemTemplate,
            (s, v) => s with { DisplayTemplatesOrNull = s.DisplayTemplates with { QueueItem = v } }),
        Field(s => s.DisplayTemplates.HistoryItem, v => HistoryItemTemplate = v, x => x.HistoryItemTemplate,
            (s, v) => s with { DisplayTemplatesOrNull = s.DisplayTemplates with { HistoryItem = v } }),
        Field(s => s.QueueCutoffEnabled, v => QueueCutoffEnabled = v, x => x.QueueCutoffEnabled,
            (s, v) => s with { QueueCutoffEnabled = v }),
        Field(s => s.QueueCutoffMinutesOfDay, v => QueueCutoffMinutesOfDay = v, x => x.QueueCutoffMinutesOfDay,
            (s, v) => s with { QueueCutoffMinutesOfDay = v }),
        Field(s => s.QueueCutoffGraceMinutes, v => QueueCutoffGraceMinutes = v, x => x.QueueCutoffGraceMinutes,
            (s, v) => s with { QueueCutoffGraceMinutes = v }),
        Field(s => s.EndOfNightAudioPath, v => EndOfNightAudioPath = v, x => x.EndOfNightAudioPath,
            (s, v) => s with { EndOfNightAudioPath = v }),
        Field(s => s.PlayEndOfNightAtCutoff, v => PlayEndOfNightAtCutoff = v, x => x.PlayEndOfNightAtCutoff,
            (s, v) => s with { PlayEndOfNightAtCutoff = v }),
        Field(s => s.WebServerEnabled, v => WebServerEnabled = v, x => x.WebServerEnabled, (s, v) => s with { WebServerEnabled = v }),
        Field(s => s.WebServerPort, v => WebServerPort = v, x => x.WebServerPort, (s, v) => s with { WebServerPort = v }),
        // Switching the remote on for the first time mints its PIN, so there is never a moment
        // where the remote is reachable and the PIN is empty.
        Field(s => s.WebRemoteControlEnabled, v => WebRemoteControlEnabled = v, x => x.WebRemoteControlEnabled,
            (s, v) => s with
            {
                WebRemoteControlEnabled = v,
                WebRemoteControlPin = v && s.WebRemoteControlPin.Length == 0
                    ? RemoteAccessService.GeneratePin()
                    : s.WebRemoteControlPin
            }),
        Field(s => s.WebRemoteControlPin, v => WebRemoteControlPin = v),
        Field(s => s.ApplicationTheme, v => SelectedTheme = v, x => x.SelectedTheme, (s, v) => s with { ApplicationTheme = v }),
        Field(s => s.ApplicationLanguage, v => SelectedLanguage = v)
    ];

    private SettingField Field<T>(
        Func<ApplicationSettings, T> read,
        Action<T> show,
        System.Linq.Expressions.Expression<Func<SettingsViewModel, T>>? property = null,
        Func<ApplicationSettings, T, ApplicationSettings>? write = null)
    {
        void SaveWhenChanged()
        {
            if (property is not null && write is not null)
            {
                ThrottledSave(property, value => settings => write(settings, value));
            }
        }

        return new SettingField(settings => show(read(settings)), SaveWhenChanged);
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
            .Throttle(TimeSpan.FromMilliseconds(300), _saveScheduler)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(value => CommitDirectAsync(transform(value)).SafeFireAndForget(exception =>
                _loggerService.Report(
                    "Failed to save settings", _notifications, DomainStrings.Settings_SaveFailed, exception)))
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
            _loggerService.Report("Failed to save settings", _notifications, DomainStrings.Settings_SaveFailed, ex);
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
            _loggerService.Report(
                "Failed to change language", _notifications, UiStrings.Settings_LanguageChangeFailed, ex);
        }
    }

    public void Dispose() => _disposables.Dispose();
}
