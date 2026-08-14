namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>What came of asking for a newer list.</summary>
/// <remarks>
/// A failed update is not an error state for the application: the list it already had is still
/// perfectly good, and an evening in a hall with no wifi should not look broken.
/// </remarks>
public sealed record DanceListUpdate(DanceListUpdateOutcome Outcome, int DancesAdded, string? Problem)
{
    public static DanceListUpdate Unchanged { get; } = new(DanceListUpdateOutcome.AlreadyCurrent, 0, null);

    public static DanceListUpdate Updated(int dancesAdded) =>
        new(DanceListUpdateOutcome.Updated, dancesAdded, null);

    public static DanceListUpdate Failed(string problem) =>
        new(DanceListUpdateOutcome.Failed, 0, problem);
}

public enum DanceListUpdateOutcome
{
    Updated,
    AlreadyCurrent,
    Failed
}
