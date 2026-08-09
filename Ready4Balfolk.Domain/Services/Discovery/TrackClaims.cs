using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;

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
/// Claims of one field come out in the order they are trusted, most trusted first. That order is
/// the whole of the "tag trust" question for now: album artist before artist, title tag before
/// file name. Nothing is read out of a folder name or a file name field, because what a level or a
/// field means is something the user declares and not something a library can be asked.
/// </para>
/// </remarks>
public static partial class TrackClaims
{
    /// <summary>Everything said about a file, by everything that said anything.</summary>
    /// <param name="evidence">What the file offered.</param>
    /// <param name="index">The user's dance list, which is the only vocabulary any of this has.</param>
    /// <param name="folderDance">
    /// The dance the rest of the folder turned out to be, when it agreed on one.
    /// </param>
    public static IReadOnlyList<Claim> Collect(
        TrackEvidence evidence, DanceListIndex index, string? folderDance = null)
    {
        var claims = new List<Claim>();

        AddDanceClaims(claims, evidence, index, folderDance);
        AddArtistClaims(claims, evidence);
        AddTitleClaims(claims, evidence);

        return claims;
    }

    private static void AddDanceClaims(
        List<Claim> claims, TrackEvidence evidence, DanceListIndex index, string? folderDance)
    {
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

        // Tag fields are read one at a time rather than as one blob, because which field a dance was
        // written into is a thing the user will want to declare, and a blob cannot be declared over.
        foreach (var (field, text) in TagFields(evidence))
        {
            foreach (var (_, matchedName) in DanceNameScanner.Scan(text, index))
            {
                claims.Add(Dance(matchedName, ClaimSource.Tag(field)));
            }
        }

        if (folderDance is not null)
        {
            claims.Add(Dance(index.DisplayNameFor(folderDance), ClaimSource.FolderAgreement));
        }
    }

    private static void AddArtistClaims(List<Claim> claims, TrackEvidence evidence)
    {
        // Album artist first: on a compilation the performer changes per track and the album artist
        // is the thing the user filed. Placeholders are claimed anyway and refused when deciding,
        // so that "the artist tag says Unknown Artist" stays visible instead of looking like silence.
        AddIfSaid(claims, TrackField.Artist, evidence.TagAlbumArtist, ClaimSource.Tag("album artist"));
        AddIfSaid(claims, TrackField.Artist, evidence.TagArtist, ClaimSource.Tag("artist"));
    }

    private static void AddTitleClaims(List<Claim> claims, TrackEvidence evidence)
    {
        AddIfSaid(claims, TrackField.Title, evidence.TagTitle, ClaimSource.Tag("title"));

        // The file name whole, with a leading track number taken off it. Which part of a name is
        // the title is exactly what an undeclared library cannot say, and a number is not a name in
        // any arrangement.
        var fromFileName = StripTrackNumber(evidence.FileNameWithoutExtension).Trim();
        AddIfSaid(
            claims,
            TrackField.Title,
            fromFileName.Length > 0 ? fromFileName : evidence.FileNameWithoutExtension,
            ClaimSource.FileName);
    }

    private static IEnumerable<(string Field, string? Text)> TagFields(TrackEvidence evidence) =>
    [
        ("title", evidence.TagTitle),
        ("album", evidence.TagAlbum),
        ("comment", evidence.TagComment)
    ];

    private static void AddIfSaid(List<Claim> claims, TrackField field, string? value, ClaimSource source)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim
            {
                Field = field,
                Value = value.Trim(),
                Source = source,
                Trust = ClaimTrust.Observed
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
