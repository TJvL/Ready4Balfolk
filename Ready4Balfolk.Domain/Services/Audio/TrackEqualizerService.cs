using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Domain.Services.Audio;

/// <summary>
/// Keeps the audio engine told which files have an equalizer of their own, and says which one is
/// playing through it.
/// </summary>
/// <remarks>
/// The whole set rather than the playing track's alone, so the track loaded ahead is shaped right
/// from the moment it is opened (see <see cref="IAudioPlaybackService.SetTrackEqualizersAsync"/>).
/// The set follows the library, and the library follows the index, so a curve given in the edit
/// dialog and one pulled on the main screen during a dance arrive by the same road.
/// </remarks>
public sealed class TrackEqualizerService : ITrackEqualizerService, IDisposable
{
    private readonly ITrackStore _trackStore;
    private readonly IQueueConsumptionService _consumption;
    private readonly ISettingsStore _settingsStore;
    private readonly IAudioPlaybackService _audio;
    private readonly ILoggerService _loggerService;
    private readonly INotificationService _notifications;
    private readonly BehaviorSubject<Track?> _inForce = new(null);
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Lock _gate = new();

    /// <summary>The library's tracks that carry a curve, switched on or not, by the path a stream is opened with.</summary>
    private Dictionary<string, Track> _withEqualizer = new(StringComparer.Ordinal);

    /// <summary>The curves in use, as the audio engine was last told them.</summary>
    private Dictionary<string, EqualizerSettings> _curves = new(StringComparer.Ordinal);

    public TrackEqualizerService(
        ITrackStore trackStore,
        IQueueConsumptionService consumption,
        ISettingsStore settingsStore,
        IAudioPlaybackService audio,
        ILoggerService loggerService,
        INotificationService notifications)
    {
        _trackStore = trackStore;
        _consumption = consumption;
        _settingsStore = settingsStore;
        _audio = audio;
        _loggerService = loggerService;
        _notifications = notifications;

        _subscriptions.Add(trackStore.Connect().Subscribe(_ => OnLibraryChanged()));
        _subscriptions.Add(consumption.WhenCurrentItemChanged.Subscribe(_ => DecideInForce()));
        _subscriptions.Add(settingsStore.Observe()
            .Select(settings => settings.Equalizer.Enabled)
            .DistinctUntilChanged()
            .Subscribe(_ => DecideInForce()));
    }

    public IObservable<Track?> WhenInForce => _inForce.AsObservable();

    public Track? InForce => _inForce.Value;

    public void Hear(Track track, EqualizerSettings equalizer)
    {
        ArgumentNullException.ThrowIfNull(track);

        lock (_gate)
        {
            _curves = new Dictionary<string, EqualizerSettings>(_curves, StringComparer.Ordinal)
            {
                [StreamPath(track)] = equalizer
            };
            Tell(_curves);
        }
    }

    public Task SaveAsync(Track track, EqualizerSettings equalizer)
    {
        ArgumentNullException.ThrowIfNull(track);

        return _trackStore.SetEqualizerAsync(track.FileInfo.FullName, equalizer);
    }

    public Task BackToGlobalAsync(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        // Switched off rather than deleted: the curve was somebody's work, and switching it back
        // on in the edit dialog finds it where it was left.
        return track.Equalizer is { } own
            ? _trackStore.SetEqualizerAsync(track.FileInfo.FullName, own with { Enabled = false })
            : Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _inForce.Dispose();
    }

    /// <summary>The path the queue opens this track's stream with, which is what the engine keys on.</summary>
    /// <remarks>
    /// Through a URI and back, because that is the road the queue takes to the audio engine, and a
    /// path that comes back spelled differently would be a curve the engine never finds.
    /// </remarks>
    private static string StreamPath(Track track) => new Uri(track.FileInfo.FullName).LocalPath;

    private void OnLibraryChanged()
    {
        var withEqualizer = _trackStore.Current
            .Where(track => track.Equalizer is not null)
            .GroupBy(StreamPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        lock (_gate)
        {
            _withEqualizer = withEqualizer;
            _curves = withEqualizer
                .Where(pair => pair.Value.HasOwnEqualizer)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Equalizer!, StringComparer.Ordinal);
            Tell(_curves);
        }

        DecideInForce();
    }

    private void DecideInForce()
    {
        // Under the gate as a whole: the library changes on a scan's thread and the queue moves on
        // the UI thread, and two answers worked out at once must not be published in the wrong order.
        lock (_gate)
        {
            Track? inForce = null;

            if (_settingsStore.Current.Equalizer.Enabled
                && AudioItems.LibraryTrackOf(_consumption.CurrentItem) is { } playing
                && _withEqualizer.TryGetValue(StreamPath(playing), out var track)
                && track.HasOwnEqualizer)
            {
                inForce = track;
            }

            if (!Equals(inForce, _inForce.Value))
            {
                _inForce.OnNext(inForce);
            }
        }
    }

    /// <summary>Hands the engine the set of curves just built. The gate must be held.</summary>
    private void Tell(Dictionary<string, EqualizerSettings> curves) =>
        _audio.SetTrackEqualizersAsync(curves)
            .SafeFireAndForget(exception => _loggerService.Report(
                "Failed to apply a track's own equalizer", _notifications,
                DomainStrings.Equalizer_TrackApplyFailed, exception));
}
