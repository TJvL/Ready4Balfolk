using Ready4Balfolk.Domain.Models.QueueItems;

namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>The file the user nominated as the end of the night, if it is still there.</summary>
/// <remarks>
/// Checked before it is offered rather than at the moment somebody presses the button in front of a
/// room: if the path stops resolving the button goes back to disabled, which is also where it starts.
/// </remarks>
public interface IEndOfNightAudio
{
    /// <summary>True when a file has been chosen and is where the settings say it is.</summary>
    bool IsAvailable { get; }

    /// <summary>The entry to queue, or null when there is no usable file.</summary>
    EndOfNightQueueItem? Create();
}
