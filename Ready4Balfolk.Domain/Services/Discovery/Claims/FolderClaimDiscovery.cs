using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>What the folder levels the user gave a role to say about this file.</summary>
/// <remarks>
/// Applied only where the depth is there. A library with three levels in one corner and one in
/// another is ordinary, and a rule firing on the files that have the depth while staying quiet
/// on the rest is the honest reading of "level 2 is the album".
/// </remarks>
public class FolderClaimDiscovery(DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null)
    {
        for (var level = 1; level <= evidence.PathSegments.Count; level++)
        {
            var field = declared.RoleForLevel(level) switch
            {
                FolderRole.Artist => TrackField.Artist,
                FolderRole.Dance => TrackField.Dance,
                // An album level is worth declaring and there is nothing to claim from it: no track
                // carries an album. Ignore and Unknown say nothing by construction.
                _ => (TrackField?)null
            };

            if (field is not null)
            {
                var claim = ClaimCreator.AddIfSaid(field.Value, evidence.PathSegments[level - 1], ClaimSource.FolderLevel(level), ClaimTrust.Declared);
                if (claim is not null)
                {
                    yield return claim;
                }
            }
        }
    }
}
