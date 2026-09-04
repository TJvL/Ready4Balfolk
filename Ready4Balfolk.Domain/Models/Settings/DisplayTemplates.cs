namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>How a track is written on each screen that writes one as a line.</summary>
/// <remarks>
/// <para>
/// One per surface, because they are not the same sentence: the line under a dance name while it is
/// playing is read from across a desk mid-evening, and a history row is read afterwards. The
/// defaults are what the application said before any of this existed, so a user who never opens the
/// setting sees no change.
/// </para>
/// <para>
/// The catalogue is not here. It is a table somebody sorts by dance, by artist or by title, and a
/// column is not a sentence.
/// </para>
/// </remarks>
public sealed record DisplayTemplates
{
    /// <summary>What every screen said before anybody could say otherwise.</summary>
    public static readonly DisplayTemplates Default = new();

    /// <summary>The large line of what is playing.</summary>
    public string NowPlayingPrimary { get; init; } = "%d";

    /// <summary>The line under it.</summary>
    public string NowPlayingSecondary { get; init; } = "%a - %t";

    /// <summary>One row of the queue.</summary>
    public string QueueItem { get; init; } = "%d - %a - %t";

    /// <summary>One row of the night's account.</summary>
    public string HistoryItem { get; init; } = "%d - %a - %t";
}
