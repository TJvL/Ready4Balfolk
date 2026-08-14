using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.QueueItems;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Domain.Services.Queue;

/// <inheritdoc cref="IEndOfNightAudio" />
public sealed class EndOfNightAudio(
    ISettingsStore settingsStore,
    IFileSystem fileSystem,
    ILoggerService loggerService) : IEndOfNightAudio
{
    public bool IsAvailable => Path is not null;

    public EndOfNightQueueItem? Create() =>
        Path is { } path ? new EndOfNightQueueItem(path, ReadDuration(path)) : null;

    private string? Path
    {
        get
        {
            var path = settingsStore.Current.EndOfNightAudioPath;
            return path.Length > 0 && fileSystem.File.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// Read when the entry is queued, so the projected end time counts it like anything else.
    /// </summary>
    /// <remarks>
    /// A file that will not say how long it is still plays; it simply contributes nothing to the
    /// projection, which is better than refusing to end the evening over a missing header.
    /// </remarks>
    private TimeSpan? ReadDuration(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path);
            var duration = file.Properties.Duration;
            return duration > TimeSpan.Zero ? duration : null;
        }
        catch (Exception exception)
        {
            _ = loggerService.ErrorAsync($"Could not read the length of '{path}'", exception);
            return null;
        }
    }
}
