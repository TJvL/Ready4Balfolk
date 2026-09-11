namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

public interface IClaimDiscovery
{
    IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null);
}