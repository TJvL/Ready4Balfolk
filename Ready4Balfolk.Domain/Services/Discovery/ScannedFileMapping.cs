using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>
/// Turns what a scan made of a file into the rows the library index stores.
/// </summary>
/// <remarks>
/// Extracted from TrackStore, which had no business knowing the shape of an index row on top of
/// everything else it does. Pure, so it is testable without a scan or a database.
/// </remarks>
public static class ScannedFileMapping
{
    private static readonly TrackField[] AllFields = [TrackField.Dance, TrackField.Artist, TrackField.Title];

    /// <summary>
    /// What the user's own rules answered on this file, which they approved by declaring them.
    /// </summary>
    /// <remarks>
    /// A field whose answer came from a declared claim is approved by that rule, and the rule is
    /// recorded so review can say which one. The dance keeps the text rather than a slug when the
    /// list does not know it: the rule did answer, the track parks on the value, and an import that
    /// carries the name releases it without anybody being asked.
    /// </remarks>
    public static IEnumerable<TrackApproval> ByRuleApprovals(ScannedFile scanned)
    {
        foreach (var field in AllFields)
        {
            var decision = scanned.Resolution.For(field);
            var chosen = decision.Chosen.FirstOrDefault(claim => claim.Trust is ClaimTrust.Declared);

            var (value, rule) = chosen is not null
                ? (decision.Value, chosen.Source.Detail)
                : decision.Reason is DecisionReason.Unusable
                    && scanned.Resolution.ClaimsFor(field).FirstOrDefault(claim => claim.Trust is ClaimTrust.Declared)
                        is { } parked
                    ? (parked.Value, parked.Source.Detail)
                    : (null, null);

            if (value is not null && rule is not null)
            {
                yield return new TrackApproval
                {
                    ContentHash = scanned.Evidence.ContentHash,
                    Field = field,
                    Value = value,
                    Kind = ApprovalKind.ByRule,
                    Rule = rule,
                    FileWriteUtc = scanned.File.LastWriteTimeUtc
                };
            }
        }
    }

    public static LibraryEntry ToEntry(ScannedFile scanned) => new()
    {
        ContentHash = scanned.Evidence.ContentHash,
        Path = scanned.File.FullName,
        FileSize = scanned.File.Length,
        LastWriteUtc = scanned.File.LastWriteTimeUtc,
        Duration = scanned.Evidence.Duration,
        Format = scanned.Evidence.Format,
        CustomTagNames = [.. scanned.Evidence.CustomTags.Keys],
        DanceSlug = scanned.Resolution.DanceSlug,
        OriginalDance = scanned.Resolution.OriginalDance,
        Artist = scanned.Resolution.Artist,
        Title = scanned.Resolution.Title,
        Dance = SourceOf(scanned.Resolution, TrackField.Dance),
        ArtistFrom = SourceOf(scanned.Resolution, TrackField.Artist),
        TitleFrom = SourceOf(scanned.Resolution, TrackField.Title)
    };

    /// <summary>What answered a field, kept so review can show it next to the value.</summary>
    private static DerivedFrom SourceOf(TrackResolution resolution, TrackField field)
    {
        var decision = resolution.For(field);
        var claim = decision.Chosen.Count > 0 ? decision.Chosen[0] : null;

        return new DerivedFrom(claim?.Source.Kind, claim?.Source.Detail, decision.Reason);
    }

}
