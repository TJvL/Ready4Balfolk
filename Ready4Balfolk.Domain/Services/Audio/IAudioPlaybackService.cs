using System.Reactive;
using Ready4Balfolk.Domain.Models.Settings;

namespace Ready4Balfolk.Domain.Services.Audio;

public interface IAudioPlaybackService
{
    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsStopped { get; }

    /// <summary>False when the BASS_FX add-on could not be loaded. Playback still works without it.</summary>
    bool IsEqualizerAvailable { get; }

    /// <summary>Applies the equalizer to the playing track and to the preloaded one.</summary>
    Task SetEqualizerAsync(EqualizerSettings equalizerSettings);

    Task SelectAsync(Uri source);
    Task PlayAsync();
    Task PauseAsync();
    Task RestartAsync();
    Task SeekAsync(TimeSpan position);
    Task ClearAsync();

    /// <summary>Lets the playing track go, and leaves the one loaded ahead of it waiting.</summary>
    Task ClearPlayingAsync();

    /// <summary>
    /// Opens the track that comes next, so that selecting it later starts it without a disk read.
    /// Asking again for the track that is already waiting keeps the stream it has.
    /// </summary>
    Task PreloadNextAsync(Uri source);
    Task ClearPreloadAsync();

    IObservable<Uri?> WhenSelectedChanged { get; }
    IObservable<Unit> WhenPlaybackStarted { get; }
    IObservable<Unit> WhenPlaybackPaused { get; }
    IObservable<Unit> WhenPlaybackRestarted { get; }
    IObservable<Unit> WhenPlaybackCleared { get; }
    IObservable<Unit> WhenPlaybackEnded { get; }
    IObservable<TimeSpan> WhenProgressChanged { get; }
    IObservable<TimeSpan> WhenDurationChanged { get; }
    IObservable<bool> WhenAvailabilityChanged { get; }
}
