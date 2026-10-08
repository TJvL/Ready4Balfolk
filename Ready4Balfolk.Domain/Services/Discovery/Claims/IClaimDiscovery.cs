namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>One thing that can speak about a file, asked what it says.</summary>
/// <remarks>
/// Each says what it says and decides nothing: deciding is <see cref="TrackInformationResolver"/>'s
/// job, over the claims of all of them together.
/// </remarks>
public interface IClaimDiscovery
{
    IEnumerable<Claim> CollectClaims(TrackEvidence evidence);
}
