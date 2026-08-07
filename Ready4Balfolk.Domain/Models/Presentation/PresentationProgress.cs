namespace Ready4Balfolk.Domain.Models.Presentation;

/// <summary>How far through the current item playback is.</summary>
/// <param name="Elapsed">How long the current item has been running.</param>
/// <param name="Duration">Its total length, or zero when it has none, as a stop does.</param>
/// <remarks>
/// Separate from <see cref="PresentationState"/> because it changes ten times a second while the
/// state around it does not, and a surface that has to push over a network samples it down.
/// </remarks>
public sealed record PresentationProgress(TimeSpan Elapsed, TimeSpan Duration)
{
    /// <summary>Nothing playing.</summary>
    public static readonly PresentationProgress Zero = new(TimeSpan.Zero, TimeSpan.Zero);

    /// <summary>How much is left, never negative.</summary>
    public TimeSpan Remaining => Duration > Elapsed ? Duration - Elapsed : TimeSpan.Zero;

    /// <summary>How far through, from 0 to 1, and zero for an item with no known length.</summary>
    public double Fraction => Duration > TimeSpan.Zero
        ? Math.Clamp(Elapsed / Duration, 0d, 1d)
        : 0d;
}
