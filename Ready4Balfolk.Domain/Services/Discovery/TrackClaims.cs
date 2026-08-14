using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Asks everything that can speak about a file what it has to say, and decides nothing.</summary>
/// <remarks>
/// <para>
/// Collecting and deciding are separate so that "what the file offered" survives the decision. A
/// claim that lost is the only way a person can see why a field reads as it does, and a claim the
/// dance list does not recognise is the thing that parks a track in review, so neither may be
/// dropped here.
/// </para>
/// <para>
/// Claims of one field come out in the order they are trusted, most trusted first: what the user
/// declared, then what the file says about itself. Nothing is read out of a folder name or a file
/// name field unless the user declared that it means something, because what a level or a field
/// means is not a thing a library can be asked.
/// </para>
/// </remarks>
public static partial class TrackClaims
{
    /// <summary>Everything said about a file, by everything that said anything.</summary>
    /// <param name="evidence">What the file offered.</param>
    /// <param name="index">The user's dance list, which is the only vocabulary any of this has.</param>
    /// <param name="declared">The rules the user stated, compiled. Undeclared by default.</param>
    /// <param name="folderDance">
    /// The dance the rest of the folder turned out to be, when it agreed on one.
    /// </param>
    public static IReadOnlyList<Claim> Collect(
        TrackEvidence evidence,
        DanceListIndex index,
        DeclaredDiscovery? declared = null,
        string? folderDance = null)
    {
        declared ??= DeclaredDiscovery.Undeclared;

        var claims = new List<Claim>();

        AddPatternClaims(claims, evidence, declared);
        AddFolderRoleClaims(claims, evidence, declared);
        AddDanceClaims(claims, evidence, index, declared, folderDance);
        AddTagClaims(claims, evidence, declared, TrackField.Artist);
        AddTagClaims(claims, evidence, declared, TrackField.Title);
        AddFileNameTitleClaim(claims, evidence);

        return claims;
    }

    /// <summary>What the first pattern to match the whole name makes of it.</summary>
    /// <remarks>
    /// A pattern is the user saying "my files are shaped like this", so what it picks out is claimed
    /// at the top tier: they have taken responsibility for the rule, and the code stops hedging.
    /// </remarks>
    private static void AddPatternClaims(List<Claim> claims, TrackEvidence evidence, DeclaredDiscovery declared)
    {
        var matched = declared.Patterns
            .Select(pattern => (Pattern: pattern, Match: pattern.Match(
                pattern.UsesExtension ? evidence.FileName : evidence.FileNameWithoutExtension)))
            .FirstOrDefault(candidate => candidate.Match is not null);

        if (matched.Match is not { } match)
        {
            return;
        }

        var source = ClaimSource.Pattern(matched.Pattern.Text);

        AddIfSaid(claims, TrackField.Dance, match.Dance, source, ClaimTrust.Declared);
        AddIfSaid(claims, TrackField.Artist, match.Artist, source, ClaimTrust.Declared);
        AddIfSaid(claims, TrackField.Title, match.Title, source, ClaimTrust.Declared);
    }

    /// <summary>What the folder levels the user gave a role to say about this file.</summary>
    /// <remarks>
    /// Applied only where the depth is there. A library with three levels in one corner and one in
    /// another is ordinary, and a rule firing on the files that have the depth while staying quiet
    /// on the rest is the honest reading of "level 2 is the album".
    /// </remarks>
    private static void AddFolderRoleClaims(List<Claim> claims, TrackEvidence evidence, DeclaredDiscovery declared)
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
                AddIfSaid(claims, field.Value, evidence.PathSegments[level - 1], ClaimSource.FolderLevel(level), ClaimTrust.Declared);
            }
        }
    }

    private static void AddDanceClaims(
        List<Claim> claims,
        TrackEvidence evidence,
        DanceListIndex index,
        DeclaredDiscovery declared,
        string? folderDance)
    {
        // A tag field the user declared holds the dance is read whole, recognised or not. That is
        // the difference between trusting a field and finding a name in it.
        var (trusted, isDeclared) = declared.TagTrust.For(TrackField.Dance);
        if (isDeclared)
        {
            foreach (var field in trusted)
            {
                AddIfSaid(claims, TrackField.Dance, ValueOf(evidence, field), ClaimSource.Tag(Name(field)), ClaimTrust.Declared);
            }
        }

        // A custom tag the user named holds the dance. Naming it is the declaration, so it is read
        // whole exactly like a trusted field: recognised or not, what it says is what parks or
        // passes the track.
        if (declared.CustomDanceTag is { } customTag && evidence.CustomTags.TryGetValue(customTag, out var customValue))
        {
            AddIfSaid(claims, TrackField.Dance, customValue, ClaimSource.Tag(customTag), ClaimTrust.Declared);
        }

        var fileName = evidence.FileNameWithoutExtension;
        var bracketed = BracketedGroups(fileName);
        var fileMatches = DanceNameScanner.Scan(fileName, index);

        foreach (var (_, matchedName) in fileMatches)
        {
            var inBrackets = bracketed.Any(group => group.Contains(matchedName, StringComparison.Ordinal));
            claims.Add(Dance(matchedName, inBrackets ? ClaimSource.Brackets : ClaimSource.FileName));
        }

        // A bracketed value nothing recognised is still somebody saying "this is the dance", and it
        // is what a review screen groups 21 identical misspellings by. Only when the name itself
        // recognised nothing: otherwise "(Mazurka)" would be claimed twice, once as itself.
        if (fileMatches.Count == 0 && TrailingBracket(fileName) is { } written)
        {
            claims.Add(Dance(written, ClaimSource.Brackets));
        }

        // Names from the list found in ordinary tag text. This is the vocabulary recognising itself
        // rather than a field being trusted, so it needs no declaration: a dance the user's own list
        // names is not a guess about what the field means.
        foreach (var field in DanceTextFields)
        {
            foreach (var (_, matchedName) in DanceNameScanner.Scan(ValueOf(evidence, field), index))
            {
                claims.Add(Dance(matchedName, ClaimSource.Tag(Name(field))));
            }
        }

        if (folderDance is not null)
        {
            claims.Add(Dance(index.DisplayNameFor(folderDance), ClaimSource.FolderAgreement));
        }
    }

    /// <summary>
    /// What the tags say about a field, in the order those fields are trusted for it.
    /// </summary>
    /// <remarks>
    /// The default order is a guess and is claimed as one: album artist before artist because a
    /// compilation changes performer per track. A user who states the order has declared it, and
    /// what it yields is claimed at the top tier.
    /// </remarks>
    private static void AddTagClaims(
        List<Claim> claims, TrackEvidence evidence, DeclaredDiscovery declared, TrackField field)
    {
        var (trusted, isDeclared) = declared.TagTrust.For(field);
        var trust = isDeclared ? ClaimTrust.Declared : ClaimTrust.Observed;

        foreach (var tagField in trusted)
        {
            AddIfSaid(claims, field, ValueOf(evidence, tagField), ClaimSource.Tag(Name(tagField)), trust);
        }
    }

    private static void AddFileNameTitleClaim(List<Claim> claims, TrackEvidence evidence)
    {
        // The file name whole, with a leading track number taken off it. Which part of a name is
        // the title is exactly what an undeclared library cannot say, and a number is not a name in
        // any arrangement.
        var stripped = StripTrackNumber(evidence.FileNameWithoutExtension).Trim();

        AddIfSaid(
            claims,
            TrackField.Title,
            stripped.Length > 0 ? stripped : evidence.FileNameWithoutExtension,
            ClaimSource.FileName,
            ClaimTrust.Observed);
    }

    /// <summary>Tag fields scanned for names from the dance list, whatever the trust settings say.</summary>
    private static IReadOnlyList<TagField> DanceTextFields { get; } =
        [TagField.Title, TagField.Album, TagField.Comment];

    private static string? ValueOf(TrackEvidence evidence, TagField field) => field switch
    {
        TagField.Title => evidence.TagTitle,
        TagField.Artist => evidence.TagArtist,
        TagField.AlbumArtist => evidence.TagAlbumArtist,
        TagField.Album => evidence.TagAlbum,
        _ => evidence.TagComment
    };

    private static string Name(TagField field) => field switch
    {
        TagField.Title => "title",
        TagField.Artist => "artist",
        TagField.AlbumArtist => "album artist",
        TagField.Album => "album",
        _ => "comment"
    };

    private static void AddIfSaid(
        List<Claim> claims, TrackField field, string? value, ClaimSource source, ClaimTrust trust)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim
            {
                Field = field,
                Value = value.Trim(),
                Source = source,
                Trust = trust
            });
        }
    }

    private static Claim Dance(string value, ClaimSource source) => new()
    {
        Field = TrackField.Dance,
        Value = value,
        Source = source,
        Trust = ClaimTrust.Observed
    };

    /// <summary>The contents of a trailing bracket, when it is not a year.</summary>
    private static string? TrailingBracket(string fileName)
    {
        var match = BracketedText().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var inside = match.Groups[1].Value.Trim();

        // "(1997)" is an edition, not a dance. Nothing was claimed, so nothing is discarded.
        return inside.Length > 0 && !LooksLikeAYear(inside) ? inside : null;
    }

    /// <summary>The folded contents of every bracketed group in the name.</summary>
    private static List<string> BracketedGroups(string fileName) =>
    [
        .. AnyBrackets().Matches(fileName)
            .Select(match => StringNormalizer.Normalize(match.Groups[1].Value))
            .Where(text => text.Length > 0)
    ];

    /// <summary>Removes a leading "07", "07.", "07-", "07 - " and the like.</summary>
    private static string StripTrackNumber(string value) => TrackNumberPrefix().Replace(value, string.Empty);

    private static bool LooksLikeAYear(string value) => value.Length == 4 && value.All(char.IsDigit);

    [GeneratedRegex(@"^\s*\d{1,3}\s*[-._)\]]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex TrackNumberPrefix();

    [GeneratedRegex(@"\(([^)]*)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedText();

    [GeneratedRegex(@"[(\[]([^)\]]*)[)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex AnyBrackets();
}
