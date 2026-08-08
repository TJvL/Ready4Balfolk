using System.Text.RegularExpressions;
using Ready4Balfolk.Domain.Models.Dances;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Turns what a file said about itself into a dance, an artist and a title.</summary>
/// <remarks>
/// <para>
/// Every source offers a candidate and none of them is trusted by position. Two independent sources
/// agreeing wins; a single source is accepted only when nothing contradicts it; and the old
/// <c>Dance - Artist - Title</c> pattern is a tiebreaker rather than the mechanism, because in a
/// real library that first field is as often a track number or a band name as a dance.
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
    /// What the rest of the album folder turned out to be, when it agreed on one dance. Used only to
    /// fill a gap: it never overrules a dance the file itself named.
    /// </param>
    public static TrackResolution Resolve(TrackEvidence evidence, DanceListIndex index, string? folderDance = null)
    {
        var fileNameMatches = DanceNameScanner.Scan(evidence.FileNameWithoutExtension, index);

        // Tags are scanned as one body of text: which field a dance was written into varies by
        // ripper, and none of them is more authoritative than another.
        var tagText = string.Join(
            " | ",
            new[] { evidence.TagTitle, evidence.TagGenre, evidence.TagAlbum, evidence.TagComment }
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

        var (slug, agreeing) = Decide(candidates, evidence, index);

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
        List<DanceCandidate> candidates, TrackEvidence evidence, DanceListIndex index)
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
            return TieBreak(corroborated, evidence, index);
        }

        return bySlug.Count == 1
            ? (bySlug[0].Slug, bySlug[0].Sources)
            : TieBreak(bySlug, evidence, index);
    }

    /// <summary>
    /// Breaks a tie with the old naming convention: whatever sits before the first " - ".
    /// </summary>
    /// <remarks>
    /// Only ever used to choose between dances the evidence already offered. Believing this field on
    /// its own is what produced a dance column full of track numbers and band names.
    /// </remarks>
    private static (string? Slug, IReadOnlyList<DanceEvidenceSource> Agreeing) TieBreak(
        List<(string Slug, List<DanceEvidenceSource> Sources)> contenders, TrackEvidence evidence, DanceListIndex index)
    {
        var leading = evidence.FileNameWithoutExtension.Split(" - ", 2);
        if (leading.Length == 2 && index.ResolveSlug(leading[0]) is { } fromPattern)
        {
            var (slug, sources) = contenders.FirstOrDefault(entry =>
                string.Equals(entry.Slug, fromPattern, StringComparison.Ordinal));
            if (slug is not null)
            {
                return (slug, sources);
            }
        }

        // Nothing to separate them, so say nothing. A person decides in the tagging editor.
        return (null, []);
    }

    /// <summary>
    /// The dance-shaped text the file offered, recognised or not.
    /// </summary>
    /// <remarks>
    /// When a name from the list was found, that is what the file said. When none was, the leading
    /// field of the old pattern is the best guess at what the file was trying to say, and it is
    /// exactly the value the tagging editor needs to group 21 identical mistakes into one decision.
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

        var parts = evidence.FileNameWithoutExtension.Split(" - ");
        if (parts.Length >= 3)
        {
            var leading = StripTrackNumber(parts[0]).Trim();
            if (leading.Length > 0)
            {
                return leading;
            }
        }

        return string.IsNullOrWhiteSpace(evidence.TagGenre) ? null : evidence.TagGenre.Trim();
    }

    /// <summary>
    /// The artist, preferring the folder the file sits in.
    /// </summary>
    /// <remarks>
    /// The library is arranged as <c>Artist/Album/track</c>, so the outermost folder is a statement
    /// the user made by filing the album there, and it is more reliable than a tag a ripper guessed.
    /// </remarks>
    private static string ResolveArtist(TrackEvidence evidence)
    {
        var fromPath = evidence.PathSegments.Count > 0 ? evidence.PathSegments[0] : null;

        var fromFileName = evidence.FileNameWithoutExtension.Split(" - ") is { Length: >= 3 } parts
            ? parts[1]
            : null;

        return ArtistNames.FirstUsable(
            fromPath,
            evidence.TagAlbumArtist,
            evidence.TagArtist,
            fromFileName) ?? string.Empty;
    }

    /// <summary>The title, with the track number and any dance in brackets taken off it.</summary>
    private static string ResolveTitle(TrackEvidence evidence)
    {
        var parts = evidence.FileNameWithoutExtension.Split(" - ");
        var raw = parts.Length >= 3
            ? string.Join(" - ", parts.Skip(2))
            : evidence.FileNameWithoutExtension;

        var cleaned = StripTrackNumber(raw).Trim();

        return cleaned.Length > 0
            ? cleaned
            : ArtistNames.FirstUsable(evidence.TagTitle) ?? evidence.FileNameWithoutExtension;
    }

    /// <summary>Removes a leading "07", "07.", "07-", "07 - " and the like.</summary>
    private static string StripTrackNumber(string value) => TrackNumberPrefix().Replace(value, string.Empty);

    private static bool LooksLikeAYear(string value) =>
        value.Length == 4 && value.All(char.IsDigit);

    [GeneratedRegex(@"^\s*\d{1,3}\s*[-._)\]]?\s*", RegexOptions.CultureInvariant)]
    private static partial Regex TrackNumberPrefix();

    [GeneratedRegex(@"\(([^)]*)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedText();
}
