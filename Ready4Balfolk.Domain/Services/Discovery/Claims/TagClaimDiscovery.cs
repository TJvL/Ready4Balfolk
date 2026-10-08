using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>
/// What the tags say about the artist and the title, in the order those fields are trusted for it.
/// </summary>
/// <remarks>
/// The default order is a guess and is claimed as one: album artist before artist because a
/// compilation changes performer per track. A user who states the order has declared it, and
/// what it yields is claimed at the top tier.
/// </remarks>
public sealed class TagClaimDiscovery(DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence) =>
        ClaimsFor(evidence, TrackField.Artist).Concat(ClaimsFor(evidence, TrackField.Title));

    private IEnumerable<Claim> ClaimsFor(TrackEvidence evidence, TrackField field)
    {
        var (trusted, isDeclared) = declared.TagTrust.For(field);
        var trust = isDeclared ? ClaimTrust.Declared : ClaimTrust.Observed;

        foreach (var tagField in trusted)
        {
            var claim = ClaimCreator.AddIfSaid(field, tagField.ValueOf(evidence), ClaimSource.Tag(tagField.Name()), trust);
            if (claim is not null)
            {
                yield return claim;
            }
        }
    }
}
