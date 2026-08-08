namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>What was decided about a track, and on what grounds.</summary>
public sealed record TrackResolution
{
    /// <summary>The dance, or null when nothing recognised one. Null is a real answer.</summary>
    public string? DanceSlug { get; init; }

    /// <summary>
    /// The dance-shaped text the file offered, whether or not it was recognised. This is what the
    /// tagging editor groups by, so 21 files claiming the same unknown thing are one decision.
    /// </summary>
    public string? OriginalDance { get; init; }

    public required string Artist { get; init; }

    public required string Title { get; init; }

    /// <summary>Which sources agreed. Two independent ones is what makes an answer trustworthy.</summary>
    public IReadOnlyList<DanceEvidenceSource> AgreeingSources { get; init; } = [];

    public bool IsResolved => DanceSlug is not null;

    /// <summary>True when more than one source agreed, rather than one having spoken alone.</summary>
    public bool IsCorroborated => AgreeingSources.Count > 1;
}
