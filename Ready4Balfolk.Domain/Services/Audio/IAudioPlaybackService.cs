using System.Reactive;

namespace Ready4Balfolk.Domain.Services.Audio;

public interface IAudioPlaybackService
{
    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsStopped { get; }
    bool AutoAdvance { get; set; }

    Task SelectAsync(Uri source);
    Task PlayAsync();
    Task PauseAsync();
    Task RestartAsync();
    Task SeekAsync(TimeSpan position);
    Task ClearAsync();

    Task PreloadNextAsync(Uri source);
    Task ClearPreloadAsync();
    Task NextAsync();

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
