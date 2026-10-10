using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using ReactiveUI.Reactive;
using ReactiveUI.SourceGenerators;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.UI.Resources;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.UI.Views.Equalizer;

/// <summary>
/// Drives the output equalizer panel.
/// </summary>
/// <remarks>
/// <para>
/// Changes go to the audio service immediately, because the whole point is to judge a room by ear
/// while a track plays, and are written to settings on a throttle so that dragging a slider does
/// not hammer the disk.
/// </para>
/// <para>
/// While a track with an equalizer of its own is playing, the sliders are that track's: what the
/// DJ hears is that curve, so it is that curve they pull, and the pull is kept on the track. The
/// switch that turns the equalizer on and off stays the global one throughout, because it is the
/// one that takes every effect out of the signal at once. The next track puts the global curve back
/// on the sliders.
/// </para>
/// </remarks>
public sealed partial class EqualizerViewModel : ReactiveObject, IDisposable
{
    private static readonly TimeSpan SaveThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly ISettingsStore _settingsStore;
    private readonly ITrackEqualizerService _trackEqualizers;
    private readonly ILoggerService _loggerService;
    private readonly INotificationService _notifications;
    private readonly IScheduler _saveScheduler;
    private readonly Subject<EqualizerSettings> _pendingSave = new();
    private readonly Subject<(Track Track, EqualizerSettings Curve)> _pendingTrackSave = new();
    private readonly CompositeDisposable _disposables = [];

    /// <summary>The curves this panel has sent the track on the sliders, so their echo is not news.</summary>
    /// <remarks>
    /// A pull is written on a throttle and comes back through the library a moment later. Taken as
    /// a change from somewhere else, the sliders would be put back to where the hand was at the
    /// write rather than where it is now, mid-drag.
    /// </remarks>
    private readonly HashSet<EqualizerSettings> _sentToTrack = [];

    private bool _syncing;

    /// <summary>The global equalizer as it stands, whatever the sliders are showing.</summary>
    private EqualizerSettings _global;

    [Reactive] public partial bool Enabled { get; set; }
    [Reactive] public partial bool LowCutEnabled { get; set; }
    [Reactive] public partial double LowCutHertz { get; set; }
    [Reactive] public partial double PreampDecibels { get; set; }
    [Reactive] public partial bool IsExpanded { get; set; }

    /// <summary>The track whose own curve is on the sliders, or null while they are the global ones.</summary>
    [Reactive] public partial Track? ShownTrack { get; private set; }

    /// <summary>Whether the sliders are a track's own rather than the global equalizer.</summary>
    [Reactive] public partial bool IsTrackEqualizer { get; private set; }

    /// <summary>What the panel is called: plain, or naming the track whose curve it is showing.</summary>
    [Reactive] public partial string Header { get; private set; }

    /// <summary>False when BASS_FX could not be loaded, which leaves the panel visible but inert.</summary>
    public bool IsAvailable { get; }

    public ObservableCollection<EqualizerBandViewModel> Bands { get; } = [];

    /// <remarks>
    /// <c>saveScheduler</c> is where the 300ms between the last slider move and the write to disk
    /// are counted. Real time unless a caller says otherwise, and only a test does: sleeping past
    /// a real throttle is the failure that passes on a quiet machine and fails on a busy one.
    /// </remarks>
    public EqualizerViewModel(
        IAudioPlaybackService audioPlaybackService,
        ISettingsStore settingsStore,
        ITrackEqualizerService trackEqualizers,
        ILoggerService loggerService,
        INotificationService notifications,
        IScheduler? saveScheduler = null)
    {
        _audioPlaybackService = audioPlaybackService;
        _settingsStore = settingsStore;
        _trackEqualizers = trackEqualizers;
        _loggerService = loggerService;
        _notifications = notifications;
        _saveScheduler = saveScheduler ?? DefaultScheduler.Instance;
        Header = UiStrings.Equalizer_Title;

        IsAvailable = audioPlaybackService.IsEqualizerAvailable;

        var current = settingsStore.Current.Equalizer;
        _global = current;
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
                .Subscribe(_ => OnCurveChanged())
                .DisposeWith(_disposables);

            Bands.Add(band);
        }

        this.WhenAnyValue(x => x.Enabled)
            .Skip(1)
            .Subscribe(_ => OnEnabledChanged())
            .DisposeWith(_disposables);

        this.WhenAnyValue(
                x => x.LowCutEnabled,
                x => x.LowCutHertz,
                x => x.PreampDecibels)
            .Skip(1)
            .Subscribe(_ => OnCurveChanged())
            .DisposeWith(_disposables);

        _pendingSave
            .Throttle(SaveThrottle, _saveScheduler)
            .Subscribe(Save)
            .DisposeWith(_disposables);

        // Throttled per track, so the last pull on one dance is still written when the next dance
        // starts and is pulled on inside the same 300ms.
        _pendingTrackSave
            .GroupBy(pending => pending.Track.FileInfo.FullName, StringComparer.Ordinal)
            .SelectMany(perTrack => perTrack.Throttle(SaveThrottle, _saveScheduler))
            .Subscribe(pending => SaveToTrack(pending.Track, pending.Curve))
            .DisposeWith(_disposables);

        trackEqualizers.WhenInForce
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(Show)
            .DisposeWith(_disposables);

        _disposables.Add(ResetToFlatCommand.ReportFailures(
            _loggerService, "Failed to reset the equalizer", _notifications, UiStrings.Equalizer_ResetFailed));
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
        OnCurveChanged();
    }

    /// <summary>Switches the playing track's own equalizer off and puts the global one back.</summary>
    /// <remarks>
    /// Off rather than deleted, so the curve is still there to switch back on in the edit dialog.
    /// Through the same throttled write as a pull, so a pull still waiting to be written cannot
    /// land after this one and switch the track's equalizer back on.
    /// </remarks>
    [ReactiveCommand]
    private void BackToGlobal()
    {
        if (ShownTrack is not { } track)
        {
            return;
        }

        var kept = ToCurve() with { Enabled = false };
        _trackEqualizers.Hear(track, kept);
        _pendingTrackSave.OnNext((track, kept));
        Show(null);
    }

    /// <summary>The sliders as a curve, switched on.</summary>
    private EqualizerSettings ToCurve() => new()
    {
        Enabled = true,
        LowCutEnabled = LowCutEnabled,
        LowCutHertz = LowCutHertz,
        PreampDecibels = PreampDecibels,
        BandGains = Bands.Select(band => band.Gain).ToArray()
    };

    /// <summary>Puts a curve on the sliders without it reading as a pull.</summary>
    private void Load(EqualizerSettings curve)
    {
        _syncing = true;

        for (var index = 0; index < Bands.Count; index++)
        {
            Bands[index].Gain = curve.BandGains[index];
        }

        PreampDecibels = curve.PreampDecibels;
        LowCutEnabled = curve.LowCutEnabled;
        LowCutHertz = curve.LowCutHertz;

        _syncing = false;
    }

    /// <summary>Shows a track's own curve on the sliders, or the global one for null.</summary>
    private void Show(Track? track)
    {
        if (track is null)
        {
            if (ShownTrack is null)
            {
                return;
            }

            ShownTrack = null;
            _sentToTrack.Clear();
            Load(_global);
        }
        else
        {
            var sameTrack = ShownTrack is { } shown
                            && string.Equals(shown.FileInfo.FullName, track.FileInfo.FullName, StringComparison.Ordinal);

            if (!sameTrack)
            {
                _sentToTrack.Clear();
            }

            ShownTrack = track;

            if (!sameTrack || !_sentToTrack.Contains(track.Equalizer!))
            {
                Load(track.Equalizer!);
            }
        }

        IsTrackEqualizer = ShownTrack is not null;
        Header = ShownTrack is { } named
            ? string.Format(CultureInfo.CurrentCulture, UiStrings.Equalizer_TrackHeader, named.Artist, named.Title)
            : UiStrings.Equalizer_Title;
    }

    private void OnEnabledChanged()
    {
        if (_syncing)
        {
            return;
        }

        // Always the global equalizer, whichever curve the sliders are showing.
        _global = _global with { Enabled = Enabled };
        ApplyAndSaveGlobal();
    }

    private void OnCurveChanged()
    {
        if (_syncing)
        {
            return;
        }

        if (ShownTrack is { } track)
        {
            var curve = ToCurve();
            _sentToTrack.Add(curve);

            // Audio first and unthrottled, exactly as for the global curve.
            _trackEqualizers.Hear(track, curve);
            _pendingTrackSave.OnNext((track, curve));
            return;
        }

        _global = ToCurve() with { Enabled = Enabled };
        ApplyAndSaveGlobal();
    }

    private void ApplyAndSaveGlobal()
    {
        // Audio first and unthrottled: the sound has to follow the slider.
        _audioPlaybackService.SetEqualizerAsync(_global)
            .SafeFireAndForget(exception => _loggerService.Report(
                "Failed to apply equalizer", _notifications, UiStrings.Equalizer_ApplyFailed, exception));

        _pendingSave.OnNext(_global);
    }

    private void Save(EqualizerSettings settings) =>
        _settingsStore.UpdateAsync(stored => stored with { EqualizerOrNull = settings })
            .SafeFireAndForget(exception => _loggerService.Report(
                "Failed to save equalizer", _notifications, UiStrings.Equalizer_SaveFailed, exception));

    private void SaveToTrack(Track track, EqualizerSettings curve) =>
        _trackEqualizers.SaveAsync(track, curve)
            .SafeFireAndForget(exception => _loggerService.Report(
                "Failed to save a track's own equalizer", _notifications, UiStrings.Equalizer_SaveFailed, exception));

    public void Dispose()
    {
        _disposables.Dispose();
        _pendingSave.Dispose();
        _pendingTrackSave.Dispose();
    }
}
