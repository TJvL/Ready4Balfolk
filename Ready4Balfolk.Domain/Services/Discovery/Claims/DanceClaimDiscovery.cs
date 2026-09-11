using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

public class DanceClaimDiscovery(DanceListIndex index, DeclaredDiscovery declared) : IClaimDiscovery
{
    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence, string? folderDance = null) =>
        DanceDeclared(evidence)
            .Concat(DanceCustomTag(evidence))
            .Concat(DanceFilename(evidence))
            .Concat(DanceNameScanner(evidence, folderDance))
    ;

    /// <summary>Tag fields scanned for names from the dance list, whatever the trust settings say.</summary>
    private static IReadOnlyList<TagField> DanceTextFields { get; } =
        [TagField.Title, TagField.Album, TagField.Comment];

    public IEnumerable<Claim> DanceNameScanner(TrackEvidence evidence, string? folderDance)
    {
        // Names from the list found in ordinary tag text. This is the vocabulary recognising itself
        // rather than a field being trusted, so it needs no declaration: a dance the user's own list
        // names is not a guess about what the field means.
        foreach (var field in DanceTextFields)
        {
            foreach (var (_, matchedName) in Discovery.DanceNameScanner.Scan(field.ValueOf(evidence), index))
            {
                yield return ClaimCreator.Dance(matchedName, ClaimSource.Tag(field.Name));
            }
        }

        if (folderDance is not null)
        {
            yield return ClaimCreator.Dance(index.DisplayNameFor(folderDance), ClaimSource.FolderAgreement);
        }
    }

    public IEnumerable<Claim> DanceFilename(TrackEvidence evidence)
    {
        var fileName = evidence.FileNameWithoutExtension;
        var bracketed = BracketGroupsExtension.BracketedGroups(fileName, index.Words);
        var fileMatches = Discovery.DanceNameScanner.Scan(fileName, index);

        foreach (var (_, matchedName) in fileMatches)
        {
            var inBrackets = bracketed.Any(group => group.Contains(matchedName, StringComparison.Ordinal));
            yield return ClaimCreator.Dance(matchedName, inBrackets ? ClaimSource.Brackets : ClaimSource.FileName);
        }

        // A bracketed value nothing recognised is still somebody saying "this is the dance", and it
        // is what a review screen groups 21 identical misspellings by. Only when the name itself
        // recognised nothing: otherwise "(Mazurka)" would be claimed twice, once as itself.
        if (fileMatches.Count == 0 && BracketGroupsExtension.TrailingBracket(fileName) is { } written)
        {
            yield return ClaimCreator.Dance(written, ClaimSource.Brackets);
        }
    }

    public IEnumerable<Claim> DanceCustomTag(TrackEvidence evidence)
    {
        // A custom tag the user named holds the dance. Naming it is the declaration, so it is read
        // whole exactly like a trusted field: recognised or not, what it says is what parks or
        // passes the track.
        if (declared.CustomDanceTag is { } customTag && evidence.CustomTags.TryGetValue(customTag, out var customValue))
        {
            var claim = ClaimCreator.AddIfSaid(TrackField.Dance, customValue, ClaimSource.Tag(customTag), ClaimTrust.Declared);
            if (claim is not null)
            {
                yield return claim;
            }
        }
    }

    public IEnumerable<Claim> DanceDeclared(TrackEvidence evidence)
    {
        // A tag field the user declared holds the dance is read whole, recognised or not. That is
        // the difference between trusting a field and finding a name in it.
        var (trusted, isDeclared) = declared.TagTrust.For(TrackField.Dance);
        if (!isDeclared)
        {
            yield break;
        }

        foreach (var field in trusted)
        {
            var claim = ClaimCreator.AddIfSaid(TrackField.Dance, field.ValueOf(evidence), ClaimSource.Tag(field.Name), ClaimTrust.Declared);
            if (claim is not null)
            {
                yield return claim;
            }
        }
    }
}
