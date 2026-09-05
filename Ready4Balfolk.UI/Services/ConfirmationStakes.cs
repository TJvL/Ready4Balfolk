namespace Ready4Balfolk.UI.Services;

/// <summary>What confirming costs, which decides where a reflex lands.</summary>
/// <remarks>
/// A dialog appears mid-set and the DJ hits return to get it out of the way. That keystroke has to
/// reach the answer that keeps the evening, so the question says what it costs and the dialog gives
/// the return key, the initial focus and the accent to the other side when the cost is real.
/// </remarks>
public enum ConfirmationStakes
{
    /// <summary>Throws something away, ends a night or interrupts the floor. Cancel answers it.</summary>
    /// <remarks>The default, so a question added later is safe before anybody thinks about it.</remarks>
    Destructive,

    /// <summary>Takes nothing away, so confirming stays the answer return and focus land on.</summary>
    Reversible
}
