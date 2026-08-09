namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>What was decided about a track, from what, and on what grounds.</summary>
/// <remarks>
/// The claims are kept alongside the decisions on purpose. A value is only reviewable if a person
/// can see what said it and what else was said instead, and a field that reads as nothing has to be
/// able to say whether that is because nobody spoke or because two sources disagreed.
/// </remarks>
public sealed record TrackResolution
{
    /// <summary>Everything every source said, winners and losers alike.</summary>
    public required IReadOnlyList<Claim> Claims { get; init; }

    public required FieldDecision DanceDecision { get; init; }

    public required FieldDecision ArtistDecision { get; init; }

    public required FieldDecision TitleDecision { get; init; }

    /// <summary>
    /// The dance-shaped text the file offered, whether or not the list recognised it. This is what
    /// the tagging editor groups by, so 21 files claiming the same unknown thing are one decision.
    /// </summary>
    public string? OriginalDance { get; init; }

    /// <summary>The dance, or null when nothing recognised one. Null is a real answer.</summary>
    public string? DanceSlug => DanceDecision.Value;

    public string Artist => ArtistDecision.Value ?? string.Empty;

    public string Title => TitleDecision.Value ?? string.Empty;

    public bool IsResolved => DanceSlug is not null;

    /// <summary>True when two independent sources agreed on the dance, rather than one speaking alone.</summary>
    public bool IsCorroborated => DanceDecision.IsCorroborated;

    public FieldDecision For(TrackField field) => field switch
    {
        TrackField.Dance => DanceDecision,
        TrackField.Artist => ArtistDecision,
        _ => TitleDecision
    };

    /// <summary>What one field was told, most trusted first.</summary>
    public IEnumerable<Claim> ClaimsFor(TrackField field) => Claims.Where(claim => claim.Field == field);
}
