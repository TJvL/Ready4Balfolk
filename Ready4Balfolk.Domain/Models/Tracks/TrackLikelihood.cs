namespace Ready4Balfolk.Domain.Models.Tracks;

/// <summary>
/// How much likelier a random pick is to land on one track than on another of the same dance.
/// </summary>
/// <remarks>
/// A multiplier on the track's share of its own dance, never on the dance's share of the pool: every
/// dance in the pool stays exactly as likely as the others, so turning one recording up cannot make
/// its dance come round more often. Whole doublings only, because "twice as likely" is a thing a DJ
/// can mean and "1.37 times" is not.
/// </remarks>
public static class TrackLikelihood
{
    public const double Lowest = 0.25;

    /// <summary>Every track until somebody says otherwise, and exactly how a pick has always behaved.</summary>
    public const double Usual = 1.0;

    public const double Highest = 4.0;

    /// <summary>A multiplier this code can draw with, whatever it was handed.</summary>
    /// <remarks>
    /// The index is a file anybody can open, and a weight of nought, a negative one or a NaN would
    /// either take a track out of the draw without a word or poison the whole dance's total. Those
    /// read as the usual likelihood; anything merely out of range is brought back into it.
    /// </remarks>
    public static double Normalize(double multiplier) =>
        double.IsFinite(multiplier) && multiplier > 0
            ? Math.Clamp(multiplier, Lowest, Highest)
            : Usual;
}
