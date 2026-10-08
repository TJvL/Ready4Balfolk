using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>What the first pattern to match the whole name makes of it.</summary>
/// <remarks>
/// A pattern is the user saying "my files are shaped like this", so what it picks out is claimed
/// at the top tier: they have taken responsibility for the rule, and the code stops hedging. The
/// match is <see cref="DeclaredDiscovery.MatchFileName"/>'s, the same one the preview a pattern is
/// approved from runs, so what was shown and what is applied cannot be two readings of one rule.
/// </remarks>
public sealed class PatternClaimDiscovery(DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence)
    {
        if (declared.MatchFileName(evidence.FileName, evidence.FileNameWithoutExtension) is not { } match)
        {
            yield break;
        }

        var source = ClaimSource.Pattern(match.Pattern);

        foreach (var (field, value) in new[]
                 {
                     (TrackField.Dance, match.Dance),
                     (TrackField.Artist, match.Artist),
                     (TrackField.Title, match.Title)
                 })
        {
            if (ClaimCreator.AddIfSaid(field, value, source, ClaimTrust.Declared) is { } claim)
            {
                yield return claim;
            }
        }
    }
}
