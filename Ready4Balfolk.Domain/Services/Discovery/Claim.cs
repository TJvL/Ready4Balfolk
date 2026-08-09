using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>One thing one source said about one field of one track.</summary>
/// <remarks>
/// <para>
/// This is the single currency discovery deals in. A claim is raw: it is what a source offered,
/// not what was decided, so a dance claim carries the text somebody wrote and not a slug. Turning
/// text into a dance is the dance list's job, and a claim the list does not recognise is still a
/// claim: it is exactly what parks a track in review.
/// </para>
/// <para>
/// Claims are never thrown away. The ones that lost are how a person sees why a field reads as it
/// does, and a wrong source is only visible if the losing claims are still there to compare with.
/// </para>
/// </remarks>
public sealed record Claim
{
    public required TrackField Field { get; init; }

    /// <summary>The text the source offered, exactly as it read.</summary>
    public required string Value { get; init; }

    public required ClaimSource Source { get; init; }

    public required ClaimTrust Trust { get; init; }
}
