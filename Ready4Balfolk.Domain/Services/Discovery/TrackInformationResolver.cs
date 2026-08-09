using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Turns what a file said about itself into a dance, an artist and a title.</summary>
/// <remarks>
/// <para>
/// Every source offers a candidate and none of them is trusted by position. Two independent sources
/// agreeing wins, and a single source is accepted only when nothing contradicts it. Nothing here
/// assumes a shape: not that a folder is an artist, not that a file name has fields in it. A library
/// root is whatever somebody's disk happens to contain, and a rule for reading one is something the
/// user declares rather than something this code guesses.
/// </para>
/// <para>
/// Answering with nothing is a legitimate outcome, and a much better one than answering with
/// something wrong: an unrecognised value goes to the tagging editor, where a person decides.
/// </para>
/// </remarks>
public static partial class TrackInformationResolver
{
    /// <summary>
    /// Decides what a track is.
    /// </summary>
    /// <param name="evidence">What the file offered.</param>
    /// <param name="index">The user's dance list.</param>
    /// <param name="folderDance">
    /// What the rest of the folder turned out to be, when it agreed on one dance. Used only to fill
    /// a gap: it never overrules a dance the file itself named.
    /// </param>
    public static TrackResolution Resolve(TrackEvidence evidence, DanceListIndex index, string? folderDance = null)
    {
        var fileNameMatches = DanceNameScanner.Scan(evidence.FileNameWithoutExtension, index);

        // Tags are scanned as one body of text: which field a dance was written into varies by
        // ripper, and none of them is more authoritative than another. Genre is not among them.
        // Measured on the reference library, a genre supplied a dance name once in 530 files, and
        // what it supplied the rest of the time was "Folk".
        var tagText = string.Join(
            " | ",
            new[] { evidence.TagTitle, evidence.TagAlbum, evidence.TagComment }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var tagMatches = DanceNameScanner.Scan(tagText, index);

        var candidates = new List<DanceCandidate>();
        candidates.AddRange(fileNameMatches.Select(m => new DanceCandidate(m.Slug, DanceEvidenceSource.FileName, m.MatchedName)));
        candidates.AddRange(tagMatches.Select(m => new DanceCandidate(m.Slug, DanceEvidenceSource.Tags, m.MatchedName)));

        // Only when the file itself named nothing. The folder is evidence about a gap, not a vote
        // against what the file said: an album of mazurkas with one scottish on it must keep the
        // scottish.
        if (folderDance is not null && candidates.Count == 0)
        {
            candidates.Add(new DanceCandidate(folderDance, DanceEvidenceSource.Folder, folderDance));
        }

        var (slug, agreeing) = Decide(candidates, evidence);

        return new TrackResolution
        {
            DanceSlug = slug,
            OriginalDance = DescribeOriginalDance(evidence, fileNameMatches, index),
            Artist = ResolveArtist(evidence),
            Title = ResolveTitle(evidence),
            AgreeingSources = agreeing
        };
    }

    private static (string? Slug, IReadOnlyList<DanceEvidenceSource> Agreeing) Decide(
        List<DanceCandidate> candidates, TrackEvidence evidence)
    {
        if (candidates.Count == 0)
        {
            return (null, []);
        }

        var bySlug = candidates
            .GroupBy(candidate => candidate.Slug, StringComparer.Ordinal)
            .Select(group => (Slug: group.Key, Sources: group.Select(c => c.Source).Distinct().ToList()))
            .ToList();

        // Two independent sources agreeing is the strongest thing available, and it beats any
        // amount of one source repeating itself.
        var corroborated = bySlug.Where(entry => entry.Sources.Count > 1).ToList();
        if (corroborated.Count == 1)
        {
            return (corroborated[0].Slug, corroborated[0].Sources);
        }

        if (corroborated.Count > 1)
        {
            // Two dances both corroborated: the file genuinely says two things, so fall to the
            // tiebreaker rather than picking the first.
            return TieBreak(corroborated, candidates, evidence);
        }

        return bySlug.Count == 1
            ? (bySlug[0].Slug, bySlug[0].Sources)
            : TieBreak(bySlug, candidates, evidence);
    }

    /// <summary>
    /// Chooses between dances the evidence already offered, or says nothing.
    /// </summary>
    private static (string? Slug, IReadOnlyList<DanceEvidenceSource> Agreeing) TieBreak(
        List<(string Slug, List<DanceEvidenceSource> Sources)> contenders,
        List<DanceCandidate> candidates,
        TrackEvidence evidence)
    {
        // Brackets first. A dance written in brackets is a deliberate statement about the track,
        // whereas an ordinary word in a title is an accident of language: "Tour" is a real dance,
        // and it should not be able to tie with the "(Mazurka)" somebody wrote on purpose.
        var bracketed = BracketedGroups(evidence.FileNameWithoutExtension);
        if (bracketed.Count > 0)
        {
            var inBrackets = contenders
                .Where(entry => candidates.Any(candidate =>
                    string.Equals(candidate.Slug, entry.Slug, StringComparison.Ordinal)
                    && bracketed.Any(group => group.Contains(candidate.MatchedName, StringComparison.Ordinal))))
                .ToList();

            if (inBrackets.Count == 1)
            {
                return (inBrackets[0].Slug, inBrackets[0].Sources);
            }
        }

        // Nothing to separate them, so say nothing. A person decides in the tagging editor.
        return (null, []);
    }

    /// <summary>
    /// The dance-shaped text the file offered, recognised or not.
    /// </summary>
    /// <remarks>
    /// When a name from the list was found, that is what the file said. When none was, a value
    /// somebody wrote in brackets is a deliberate statement about the track, and it is exactly what
    /// the tagging editor needs to group 21 identical mistakes into one decision. A field of the
    /// file name is not: whatever sits before the first " - " is as often a band or a track number
    /// as a dance, and reading it as one is how the dance column filled with band names.
    /// </remarks>
    private static string? DescribeOriginalDance(
        TrackEvidence evidence, IReadOnlyList<(string Slug, string MatchedName)> fileNameMatches, DanceListIndex index)
    {
        if (fileNameMatches.Count > 0)
        {
            return index.DisplayNameFor(fileNameMatches[0].Slug);
        }

        var bracketed = BracketedText().Match(evidence.FileNameWithoutExtension);
        if (bracketed.Success)
        {
            var inside = bracketed.Groups[1].Value.Trim();
            if (inside.Length > 0 && !LooksLikeAYear(inside))
            {
                return inside;
            }
        }

        return null;
    }

    /// <summary>The artist, from the tags and nothing else.</summary>
    /// <remarks>
    /// Where a file sits says nothing on its own. The outermost folder is an artist in one library,
    /// a country in the next and a year in a third, so until the user declares what a level means,
    /// the only claim about the artist is a tag written into an artist field.
    /// </remarks>
    private static string ResolveArtist(TrackEvidence evidence) =>
        ArtistNames.FirstUsable(evidence.TagAlbumArtist, evidence.TagArtist) ?? string.Empty;

    /// <summary>The title, from the title tag, falling back to the whole file name.</summary>
    /// <remarks>
    /// The file name is taken whole, because which of its fields is the title is exactly what no
    /// unconfigured library can be assumed to say. Only a leading track number comes off, since a
    /// number is not a name in any arrangement.
    /// </remarks>
    private static string ResolveTitle(TrackEvidence evidence)
    {
        if (ArtistNames.FirstUsable(evidence.TagTitle) is { } tagged)
        {
            return tagged;
        }

        var cleaned = StripTrackNumber(evidence.FileNameWithoutExtension).Trim();

        return cleaned.Length > 0 ? cleaned : evidence.FileNameWithoutExtension;
    }

    /// <summary>Removes a leading "07", "07.", "07-", "07 - " and the like.</summary>
    private static string StripTrackNumber(string value) => TrackNumberPrefix().Replace(value, string.Empty);

    private static bool LooksLikeAYear(string value) =>
        value.Length == 4 && value.All(char.IsDigit);

    [GeneratedRegex(@"^\s*\d{1,3}\s*[-._)\]]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex TrackNumberPrefix();

    /// <summary>The folded contents of every bracketed group in the name.</summary>
    private static List<string> BracketedGroups(string fileName) =>
    [
        .. AnyBrackets().Matches(fileName)
            .Select(match => StringNormalizer.Normalize(match.Groups[1].Value))
            .Where(text => text.Length > 0)
    ];

    [GeneratedRegex(@"\(([^)]*)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedText();

    [GeneratedRegex(@"[(\[]([^)\]]*)[)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex AnyBrackets();
}
