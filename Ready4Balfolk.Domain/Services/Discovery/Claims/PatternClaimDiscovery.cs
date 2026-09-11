using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>What the first pattern to match the whole name makes of it.</summary>
/// <remarks>
/// A pattern is the user saying "my files are shaped like this", so what it picks out is claimed
/// at the top tier: they have taken responsibility for the rule, and the code stops hedging.
/// </remarks>
public class PatternClaimDiscovery(DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null)
    {
        var (namePattern, patternMatch) = declared.Patterns
            .Select(pattern => (Pattern: pattern, Match: pattern.Match(
                pattern.UsesExtension ? evidence.FileName : evidence.FileNameWithoutExtension)))
            .FirstOrDefault(candidate => candidate.Match is not null);

        if (patternMatch is null)
        {
            yield break;
        }

        var source = ClaimSource.Pattern(namePattern.Text);

        var danceClaim = ClaimCreator.AddIfSaid(TrackField.Dance, patternMatch.Dance, source, ClaimTrust.Declared);
        if (danceClaim is not null)
        {
            yield return danceClaim;
        }

        var artistClaim = ClaimCreator.AddIfSaid(TrackField.Artist, patternMatch.Artist, source, ClaimTrust.Declared);
        if (artistClaim is not null)
        {
            yield return artistClaim;
        }

        var titleClaim = ClaimCreator.AddIfSaid(TrackField.Title, patternMatch.Title, source, ClaimTrust.Declared);
        if (titleClaim is not null)
        {
            yield return titleClaim;
        }
    }
}
