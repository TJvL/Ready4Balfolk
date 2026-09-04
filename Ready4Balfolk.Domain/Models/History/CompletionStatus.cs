namespace Ready4Balfolk.Domain.Models.History;

/// <summary>How an item stopped being the current one.</summary>
public enum CompletionStatus
{
    /// <summary>It ran out on its own.</summary>
    Finished,

    /// <summary>Somebody moved past it.</summary>
    Skipped,

    /// <summary>
    /// It could not be read, so it never played.
    /// </summary>
    /// <remarks>
    /// Distinct from being skipped, because they are not the same thing to anybody reading the
    /// night back: one is a decision, the other is a file that was not there when it was reached.
    /// </remarks>
    FileMissing
}
