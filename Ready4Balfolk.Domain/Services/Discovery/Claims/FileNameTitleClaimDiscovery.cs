using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>The file name whole as a title, the last thing anything says about one.</summary>
public sealed partial class FileNameTitleClaimDiscovery : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence)
    {
        // The file name whole, with a leading track number taken off it. Which part of a name is
        // the title is exactly what an undeclared library cannot say, and a number is not a name in
        // any arrangement.
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
