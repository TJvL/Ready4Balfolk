namespace Ready4Balfolk.Domain.Models.History;

/// <summary>One night, in the little a list of them has to show.</summary>
/// <param name="Id">Which night, for reading, exporting or throwing it away.</param>
/// <param name="StartedAt">When the first thing happened in it.</param>
/// <param name="EndedAt">When it was called, or nothing for the one still running.</param>
/// <param name="Entries">How many things happened in it.</param>
/// <remarks>
/// Summaries rather than nights, because a list of evenings is chosen from and only one of them is
/// read: an application that opened every night on the file to draw a dropdown would read a season
/// of dancing to show a date.
/// </remarks>
public sealed record NightSummary(long Id, DateTime StartedAt, DateTime? EndedAt, int Entries)
{
    /// <summary>Whether this is the night that is still running.</summary>
    public bool IsOpen => EndedAt is null;
}
