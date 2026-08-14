namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>Why the queue turned an entry away.</summary>
/// <remarks>
/// The reason a person reads is the rule's own wording; this is for the callers that have to tell
/// one no from another, and act on it rather than repeat it.
/// </remarks>
public enum QueueDenial
{
    /// <summary>Something about this entry: a duplicate, a full queue, a second auto-track.</summary>
    Entry,

    /// <summary>The queue would run past the time the evening was set to stop.</summary>
    Cutoff,

    /// <summary>The evening has been declared over and the queue is closed.</summary>
    EveningEnded
}
