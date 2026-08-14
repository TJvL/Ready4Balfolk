using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Services.Queue;

namespace Ready4Balfolk.Domain.Services.Audio;

/// <summary>Plays a single file so a person can hear what it is.</summary>
/// <remarks>
/// <para>
/// Nobody can tell a Berry from an Auvergnate by looking at a filename, so tagging without listening
/// is guessing. This exists to make that possible, and for nothing else.
/// </para>
/// <para>
/// There is one output and the room is on it. So a preview is refused outright while the queue is
/// playing rather than interrupting it: this is preparation work, and a mis-click during a night
/// would be audible to everybody in the hall.
/// </para>
/// </remarks>
public sealed class PreviewPlaybackService : IPreviewPlaybackService, IDisposable
{
    private readonly IAudioPlaybackService _playback;
    private readonly IQueueConsumptionService _consumption;
    private readonly BehaviorSubject<string?> _previewing = new(null);
    private readonly IDisposable _endedSubscription;
    private readonly IDisposable _takenOverSubscription;

    public PreviewPlaybackService(IAudioPlaybackService playback, IQueueConsumptionService consumption)
    {
        _playback = playback;
        _consumption = consumption;

        // A preview that runs to its end stops being a preview, so the strip closes itself rather
        // than sitting there claiming to play something finished.
        _endedSubscription = playback.WhenPlaybackEnded
            .Where(_ => _previewing.Value is not null)
            .Subscribe(_ => _previewing.OnNext(null));

        // The queue taking the output ends the preview as a fact, whatever this service believes.
        // Kept in step here, because a stale "previewing" later turns StopAsync into clearing the
        // track the whole room is dancing to.
        _takenOverSubscription = consumption.WhenCurrentItemChanged
            .Where(item => item is not null && _previewing.Value is not null)
            .Subscribe(_ => _previewing.OnNext(null));
    }

    public IObservable<string?> WhenPreviewChanged => _previewing.AsObservable();

    public IObservable<TimeSpan> WhenProgressChanged => _playback.WhenProgressChanged;

    public IObservable<TimeSpan> WhenDurationChanged => _playback.WhenDurationChanged;

    public string? Previewing => _previewing.Value;

    /// <summary>False while the queue owns the output, which it does whenever a night is running.</summary>
    public bool CanPreview => _consumption.CurrentItem is null;

    public async Task<bool> PlayAsync(string path)
    {
        if (!CanPreview)
        {
            return false;
        }

        await _playback.SelectAsync(new Uri(path));
        await _playback.PlayAsync();
        _previewing.OnNext(path);
        return true;
    }

    public async Task StopAsync()
    {
        if (_previewing.Value is null)
        {
            return;
        }

        _previewing.OnNext(null);

        // Cleared rather than paused: the queue takes this same output next, and it must not
        // inherit a track nobody queued. Only while the queue does not already own the output:
        // clearing then would silence the room.
        if (_consumption.CurrentItem is null)
        {
            await _playback.ClearAsync();
        }
    }

    public Task SeekAsync(TimeSpan position) =>
        _previewing.Value is null ? Task.CompletedTask : _playback.SeekAsync(position);

    public void Dispose()
    {
        _endedSubscription.Dispose();
        _takenOverSubscription.Dispose();
        _previewing.Dispose();
    }
}
