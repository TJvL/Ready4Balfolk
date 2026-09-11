using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

public partial class FilenameTitleDiscovery() : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null)
    {
        var stripped = StripTrackNumber(evidence.FileNameWithoutExtension).Trim();

        var claim = ClaimCreator.AddIfSaid(
            TrackField.Title,
            stripped.Length > 0 ? stripped : evidence.FileNameWithoutExtension,
            ClaimSource.FileName,
            ClaimTrust.Observed);

        if (claim is not null)
        {
            yield return claim;
        }
    }

    /// <summary>Removes a leading "07", "07.", "07-", "07 - " and the like.</summary>
    private static string StripTrackNumber(string value) => TrackNumberPrefix().Replace(value, string.Empty);

    [GeneratedRegex(@"^\s*\d{1,3}\s*[-._)\]]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex TrackNumberPrefix();
}

/// <summary>
/// What the tags say about a field, in the order those fields are trusted for it.
/// </summary>
/// <remarks>
/// The default order is a guess and is claimed as one: album artist before artist because a
/// compilation changes performer per track. A user who states the order has declared it, and
/// what it yields is claimed at the top tier.
/// </remarks>
public class TagClaimDiscovery(DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null)
    {
        var artist = AddTagClaims(evidence, declared, TrackField.Artist);
        var title = AddTagClaims(evidence, declared, TrackField.Title);

        return artist.Concat(title);
    }

    private static IEnumerable<Claim> AddTagClaims(
        TrackEvidence evidence, DeclaredDiscovery declared, TrackField field)
    {
        var (trusted, isDeclared) = declared.TagTrust.For(field);
        var trust = isDeclared ? ClaimTrust.Declared : ClaimTrust.Observed;

        foreach (var tagField in trusted)
        {
            var claim = ClaimCreator.AddIfSaid(field, tagField.ValueOf(evidence), ClaimSource.Tag(tagField.Name), trust);
            if (claim is not null)
            {
                yield return claim;
            }
        }
    }
}
