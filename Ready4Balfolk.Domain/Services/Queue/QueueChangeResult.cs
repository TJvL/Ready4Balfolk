namespace Ready4Balfolk.Domain.Services.Queue;

/// <summary>What became of a change asked of the queue.</summary>
/// <remarks>
/// Telling <see cref="Gone"/> from <see cref="Refused"/> is the whole point of it. A refusal is
/// about the row and stays true however long the caller looks at it; a row that has gone means the
/// list the caller was reading is out of date, and the honest answer is to look again rather than
/// to say the queue said no.
/// </remarks>
public enum QueueChangeResult
{
    /// <summary>The queue now reads as it was asked to.</summary>
    Done,

    /// <summary>
    /// The row is not in the queue any more. It played, or somebody else took it out, between the
    /// list the request was made against and the queue it arrived at.
    /// </summary>
    Gone,

    /// <summary>The row is there and the queue will not do this to it.</summary>
    Refused
}
