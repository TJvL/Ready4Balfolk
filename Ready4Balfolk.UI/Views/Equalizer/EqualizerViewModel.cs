using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.UI.Views.Equalizer;

/// <summary>
/// Drives the output equalizer panel.
/// </summary>
/// <remarks>
/// Changes go to the audio service immediately, because the whole point is to judge a room by ear
/// while a track plays, and are written to settings on a throttle so that dragging a slider does
/// not hammer the disk.
/// </remarks>
public sealed partial class EqualizerViewModel : ReactiveObject, IDisposable
{
    private static readonly TimeSpan SaveThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly ISettingsStore _settingsStore;
    private readonly ILoggerService _loggerService;
    private readonly Subject<EqualizerSettings> _pendingSave = new();
    private readonly CompositeDisposable _disposables = [];

    private bool _syncing;

    [Reactive] public partial bool Enabled { get; set; }
    [Reactive] public partial bool LowCutEnabled { get; set; }
    [Reactive] public partial double LowCutHertz { get; set; }
    [Reactive] public partial double PreampDecibels { get; set; }
    [Reactive] public partial bool IsExpanded { get; set; }

    /// <summary>False when BASS_FX could not be loaded, which leaves the panel visible but inert.</summary>
    public bool IsAvailable { get; }

    public ObservableCollection<EqualizerBandViewModel> Bands { get; } = [];

    public EqualizerViewModel(
        IAudioPlaybackService audioPlaybackService,
        ISettingsStore settingsStore,
        ILoggerService loggerService)
    {
        _audioPlaybackService = audioPlaybackService;
        _settingsStore = settingsStore;
        _loggerService = loggerService;

        IsAvailable = audioPlaybackService.IsEqualizerAvailable;

        var current = settingsStore.Current.Equalizer;
        Enabled = current.Enabled;
        LowCutEnabled = current.LowCutEnabled;
        LowCutHertz = current.LowCutHertz;
        PreampDecibels = current.PreampDecibels;

        for (var index = 0; index < EqualizerSettings.BandCenterFrequencies.Count; index++)
        {
            // Gain is set before the subscription so restoring the stored curve does not read as
            // a user edit and trigger a save.
            var band = new EqualizerBandViewModel(EqualizerSettings.BandCenterFrequencies[index])
            {
                Gain = current.BandGains[index]
            };

            band.WhenAnyValue(x => x.Gain)
                .Skip(1)
                .Subscribe(_ => OnChanged())
                .DisposeWith(_disposables);

            Bands.Add(band);
        }

        this.WhenAnyValue(
                x => x.Enabled,
                x => x.LowCutEnabled,
                x => x.LowCutHertz,
                x => x.PreampDecibels)
            .Skip(1)
            .Subscribe(_ => OnChanged())
            .DisposeWith(_disposables);

        _pendingSave
            .Throttle(SaveThrottle)
            .Subscribe(Save)
            .DisposeWith(_disposables);
    }

    [ReactiveCommand]
    private void ResetToFlat()
    {
        // Suppressed while the individual controls are reset, so one command is one apply and one
        // save rather than eleven of each.
        _syncing = true;

        foreach (var band in Bands)
        {
            band.Gain = 0;
        }

        PreampDecibels = 0;
        LowCutEnabled = false;
        LowCutHertz = EqualizerSettings.MinimumLowCutHertz;

        _syncing = false;
        OnChanged();
    }

    /// <summary>Current control values as a settings record, clamped by the record itself.</summary>
    private EqualizerSettings ToSettings() => new()
    {
        Enabled = Enabled,
        LowCutEnabled = LowCutEnabled,
        LowCutHertz = LowCutHertz,
        PreampDecibels = PreampDecibels,
        BandGains = Bands.Select(band => band.Gain).ToArray()
    };

    private void OnChanged()
    {
        if (_syncing)
        {
            return;
        }

        var settings = ToSettings();

        // Audio first and unthrottled: the sound has to follow the slider.
        _audioPlaybackService.SetEqualizerAsync(settings)
            .SafeFireAndForget(exception => _loggerService.ErrorAsync("Failed to apply equalizer", exception));

        _pendingSave.OnNext(settings);
    }

    private void Save(EqualizerSettings settings) =>
        _settingsStore.UpdateAsync(stored => stored with { EqualizerOrNull = settings })
            .SafeFireAndForget(exception => _loggerService.ErrorAsync("Failed to save equalizer", exception));

    public void Dispose()
    {
        _disposables.Dispose();
        _pendingSave.Dispose();
    }
}
