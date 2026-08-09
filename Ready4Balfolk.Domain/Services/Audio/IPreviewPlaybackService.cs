namespace Ready4Balfolk.Domain.Services.Audio;

public interface IPreviewPlaybackService
{
    /// <summary>The file being previewed, or null. One at a time, deliberately.</summary>
    IObservable<string?> WhenPreviewChanged { get; }

    IObservable<TimeSpan> WhenProgressChanged { get; }

    IObservable<TimeSpan> WhenDurationChanged { get; }

    string? Previewing { get; }

    /// <summary>False while the queue owns the one audio output.</summary>
    bool CanPreview { get; }

    /// <summary>Starts a preview. Returns false when the room is listening to something else.</summary>
    Task<bool> PlayAsync(string path);

    Task StopAsync();

    Task SeekAsync(TimeSpan position);
}
