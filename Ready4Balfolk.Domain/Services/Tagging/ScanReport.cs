namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>What a scan found, said once.</summary>
/// <remarks>
/// One report at the end, not a message per file. A scan of a real library touches thousands of
/// files, and announcing each one turns information into noise that gets dismissed without reading.
/// </remarks>
public sealed record ScanReport
{
    public static ScanReport Empty { get; } = new();

    /// <summary>Files that resolved to a dance.</summary>
    public int Complete { get; init; }

    /// <summary>Files that could not be read at all.</summary>
    public int Unreadable { get; init; }

    /// <summary>Files skipped because the format is not supported.</summary>
    public int Unsupported { get; init; }

    /// <summary>The distinct values nothing recognised, most tracks first.</summary>
    public IReadOnlyList<UnrecognisedValue> Unrecognised { get; init; } = [];

    /// <summary>Files with no dance and nothing to say about it either.</summary>
    public int SilentlyUnresolved { get; init; }

    public int UnrecognisedTrackCount => Unrecognised.Sum(value => value.TrackCount);

    /// <summary>Whether there is anything to show a person.</summary>
    public bool HasAnythingToReport =>
        Unrecognised.Count > 0 || Unreadable > 0 || Unsupported > 0 || SilentlyUnresolved > 0;
}
