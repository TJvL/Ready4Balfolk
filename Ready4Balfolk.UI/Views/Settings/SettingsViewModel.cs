using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.UI.Views.Settings;

public sealed partial class SettingsViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILoggerService _loggerService;
    private readonly CompositeDisposable _disposables = [];
    private bool _syncing;

    [Reactive] public partial string MusicDirectoryPath { get; set; }
    [Reactive] public partial int MaxQueueItems { get; set; }
    [Reactive] public partial int DelaySeconds { get; set; }
    [Reactive] public partial int PresentationDisplayCount { get; set; }
    [Reactive] public partial bool AutoQueueRandomTrack { get; set; }
    [Reactive] public partial bool AllowDuplicateTracksInQueue { get; set; }
    [Reactive] public partial bool RequirePlaybackConfirmation { get; set; }
    [Reactive] public partial ApplicationTheme SelectedTheme { get; set; }

    public IReadOnlyList<ApplicationTheme> AvailableThemes { get; } =
        Enum.GetValues<ApplicationTheme>();

    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var info = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return info is null || info.Contains("-dev") ? "dev" : info;
    }

    public SettingsViewModel(ISettingsStore settingsStore, ILoggerService loggerService)
    {
        _settingsStore = settingsStore;
        _loggerService = loggerService;

        var current = settingsStore.Current;
        MusicDirectoryPath = current.MusicDirectoryPath;
        MaxQueueItems = current.MaxQueueItems;
        DelaySeconds = current.DelaySeconds;
        PresentationDisplayCount = current.PresentationDisplayCount;
        AutoQueueRandomTrack = current.AutoQueueRandomTrack;
        AllowDuplicateTracksInQueue = current.AllowDuplicateTracksInQueue;
        RequirePlaybackConfirmation = current.RequirePlaybackConfirmation;
        SelectedTheme = current.ApplicationTheme;

        ThrottledSave(x => x.MusicDirectoryPath, v => s => s with { MusicDirectoryPath = v });
        ThrottledSave(x => x.MaxQueueItems, v => s => s with { MaxQueueItems = v });
        ThrottledSave(x => x.DelaySeconds, v => s => s with { DelaySeconds = v });
        ThrottledSave(x => x.PresentationDisplayCount, v => s => s with { PresentationDisplayCount = v });
        ThrottledSave(x => x.AutoQueueRandomTrack, v => s => s with { AutoQueueRandomTrack = v });
        ThrottledSave(x => x.AllowDuplicateTracksInQueue, v => s => s with { AllowDuplicateTracksInQueue = v });
        ThrottledSave(x => x.RequirePlaybackConfirmation, v => s => s with { RequirePlaybackConfirmation = v });
        ThrottledSave(x => x.SelectedTheme, v => s => s with { ApplicationTheme = v });

        settingsStore.Observe()
            .Skip(1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(SyncFromStore)
            .DisposeWith(_disposables);
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
        SelectedTheme = s.ApplicationTheme;
        _syncing = false;
    }

    private void ThrottledSave<T>(
        System.Linq.Expressions.Expression<Func<SettingsViewModel, T>> property,
        Func<T, Func<ApplicationSettings, ApplicationSettings>> transform)
    {
        this.WhenAnyValue(property)
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(value => CommitDirect(transform(value)))
            .DisposeWith(_disposables);
    }

    private async void CommitDirect(Func<ApplicationSettings, ApplicationSettings> transform)
    {
        if (_syncing)
            return;
        await _settingsStore.UpdateAsync(transform);
    }

    public async Task ExportLogAsync(FileInfo file) => await _loggerService.ExportAsync(file);

    public void Dispose() => _disposables.Dispose();
}
