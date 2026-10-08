using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>Everything that names a dance for a file, declared or found.</summary>
/// <remarks>
/// The declared ones first: a tag field the user said holds the dance, and their custom tag, both
/// read whole. Then what the vocabulary recognises of itself: a name from the list in the file
/// name or in ordinary tag text, a bracketed value nothing recognised, and what the rest of the
/// folder agreed on.
/// </remarks>
/// <param name="index">The user's dance list, which is the only vocabulary any of this has.</param>
/// <param name="declared">The rules the user stated, compiled.</param>
/// <param name="folderDance">The dance the rest of the folder turned out to be, when it agreed on one.</param>
public sealed class DanceClaimDiscovery(DanceListIndex index, DeclaredDiscovery declared, string? folderDance)
    : IClaimDiscovery
{
    /// <summary>Tag fields scanned for names from the dance list, whatever the trust settings say.</summary>
    private static IReadOnlyList<TagField> DanceTextFields { get; } =
        [TagField.Title, TagField.Album, TagField.Comment];

    public IEnumerable<Claim> CollectClaims(TrackEvidence evidence) =>
        DeclaredFields(evidence)
            .Concat(CustomTag(evidence))
            .Concat(FileName(evidence))
            .Concat(TagText(evidence))
            .Concat(FolderAgreement());

    private IEnumerable<Claim> DeclaredFields(TrackEvidence evidence)
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
            if (ClaimCreator.AddIfSaid(TrackField.Dance, field.ValueOf(evidence), ClaimSource.Tag(field.Name()), ClaimTrust.Declared) is { } claim)
            {
                yield return claim;
            }
        }
    }

    private IEnumerable<Claim> CustomTag(TrackEvidence evidence)
    {
        // A custom tag the user named holds the dance. Naming it is the declaration, so it is read
        // whole exactly like a trusted field: recognised or not, what it says is what parks or
        // passes the track.
        if (declared.CustomDanceTag is { } customTag
            && evidence.CustomTags.TryGetValue(customTag, out var customValue)
            && ClaimCreator.AddIfSaid(TrackField.Dance, customValue, ClaimSource.Tag(customTag), ClaimTrust.Declared) is { } claim)
        {
            yield return claim;
        }
    }

    private IEnumerable<Claim> FileName(TrackEvidence evidence)
    {
        var fileName = evidence.FileNameWithoutExtension;
        var bracketed = BracketGroups.In(fileName, index.Words);
        var fileMatches = DanceNameScanner.Scan(fileName, index);

        foreach (var (_, matchedName) in fileMatches)
        {
            // Whole words, as the scanner matched them. A substring made "Tour" bracketed in
            // "La Tour (Valse de Tournai)", and two deliberate dances then resolved to nothing.
            var inBrackets = bracketed.Any(group => WholeWords.Contains(group, matchedName));
            yield return ClaimCreator.Dance(matchedName, inBrackets ? ClaimSource.Brackets : ClaimSource.FileName);
        }

        // A bracketed value nothing recognised is still somebody saying "this is the dance", and it
        // is what a review screen groups 21 identical misspellings by. Only when the name itself
        // recognised nothing: otherwise "(Mazurka)" would be claimed twice, once as itself.
        if (fileMatches.Count == 0 && BracketGroups.Trailing(fileName) is { } written)
        {
            yield return ClaimCreator.Dance(written, ClaimSource.Brackets);
        }
    }

    private IEnumerable<Claim> TagText(TrackEvidence evidence)
    {
        // Names from the list found in ordinary tag text. This is the vocabulary recognising itself
        // rather than a field being trusted, so it needs no declaration: a dance the user's own list
        // names is not a guess about what the field means.
        foreach (var field in DanceTextFields)
        {
            foreach (var (_, matchedName) in DanceNameScanner.Scan(field.ValueOf(evidence), index))
            {
                yield return ClaimCreator.Dance(matchedName, ClaimSource.Tag(field.Name()));
            }
        }
    }

    private IEnumerable<Claim> FolderAgreement()
    {
        if (folderDance is not null)
        {
            yield return ClaimCreator.Dance(index.DisplayNameFor(folderDance), ClaimSource.FolderAgreement);
        }
    }
}
