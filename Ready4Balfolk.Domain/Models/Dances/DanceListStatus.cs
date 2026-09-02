namespace Ready4Balfolk.Domain.Models.Dances;

/// <summary>Where the list in hand came from and when, which is what the panel says out loud.</summary>
public sealed record DanceListStatus(
    int DanceCount,
    int TagCount,
    DanceListOrigin Origin,
    DateTimeOffset? ObtainedAt)
{
    public static DanceListStatus Unknown { get; } = new(0, 0, DanceListOrigin.None, null);
}

public enum DanceListOrigin
{
    /// <summary>Nothing yet. The application ships no list, so this is where every machine starts.</summary>
    None,

    /// <summary>A copy downloaded earlier and kept on disk.</summary>
    Cached,

    /// <summary>Downloaded just now.</summary>
    Downloaded,

    /// <summary>Taken from a file the user picked, for a machine that is never online.</summary>
    File
}
