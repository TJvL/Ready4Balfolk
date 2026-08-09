using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Turns what everything said about a file into a dance, an artist and a title.</summary>
/// <remarks>
/// <para>
/// Deciding is a pure function of the claims plus the dance list, so it re-runs whenever either
/// changes and is tested without a file existing at all. Nothing here assumes a shape: not that a
/// folder is an artist, not that a file name has fields in it. A rule for reading a library is
/// something the user declares, and until they do, agreement between sources is all there is.
/// </para>
/// <para>
/// Answering with nothing is a legitimate outcome and a much better one than answering with
/// something wrong: the track waits for a person instead of quietly reading as a lie.
/// </para>
/// </remarks>
public static class TrackInformationResolver
{
    /// <summary>Collects what the file says and decides what it is.</summary>
    /// <param name="evidence">What the file offered.</param>
    /// <param name="index">The user's dance list.</param>
    /// <param name="declared">The rules the user stated, compiled. Undeclared by default.</param>
    /// <param name="folderDance">
    /// What the rest of the folder turned out to be, when it agreed on one dance. Fills a gap only:
    /// it never overrules a dance the file itself named.
    /// </param>
    public static TrackResolution Resolve(
        TrackEvidence evidence,
        DanceListIndex index,
        DeclaredDiscovery? declared = null,
        string? folderDance = null) =>
        Decide(TrackClaims.Collect(evidence, index, declared, folderDance), index);

    /// <summary>Decides every field from claims alone, keeping all of them.</summary>
    public static TrackResolution Decide(IReadOnlyList<Claim> claims, DanceListIndex index)
    {
        var danceClaims = MostTrusted(claims, TrackField.Dance);
        var dance = DecideDance(danceClaims, index);

        return new TrackResolution
        {
            Claims = claims,
            DanceDecision = dance,
            ArtistDecision = DecideInOrder(TrackField.Artist, MostTrusted(claims, TrackField.Artist)),
            TitleDecision = DecideInOrder(TrackField.Title, MostTrusted(claims, TrackField.Title)),
            OriginalDance = DescribeDance(dance, danceClaims, index)
        };
    }

    /// <summary>
    /// The claims about a field from the most trusted tier that said anything about it.
    /// </summary>
    /// <remarks>
    /// A tier is not a vote to be weighed against another. A user who declares a rule has taken
    /// responsibility for it, so a declaration does not argue with what a ripper wrote: it replaces
    /// it, and the tags are still in the claim list for a person to see.
    /// </remarks>
    private static List<Claim> MostTrusted(IReadOnlyList<Claim> claims, TrackField field)
    {
        var forField = claims.Where(claim => claim.Field == field).ToList();
        if (forField.Count == 0)
        {
            return forField;
        }

        var best = forField.Max(claim => claim.Trust);
        return [.. forField.Where(claim => claim.Trust == best)];
    }

    /// <summary>
    /// Decides the dance, which is the one field with a real vocabulary behind it.
    /// </summary>
    /// <remarks>
    /// Agreement between two independent sources wins. One source alone still answers when nothing
    /// contradicts it. Two dances with nothing to separate them answer nothing at all, because a
    /// confident guess between them is the failure this whole feature exists to prevent.
    /// </remarks>
    private static FieldDecision DecideDance(List<Claim> claims, DanceListIndex index)
    {
        if (claims.Count == 0)
        {
            return new FieldDecision { Field = TrackField.Dance, Reason = DecisionReason.NoClaim };
        }

        var recognised = claims
            .Select(claim => (Claim: claim, Slug: index.ResolveSlug(claim.Value)))
            .Where(candidate => candidate.Slug is not null)
            .Select(candidate => (candidate.Claim, Slug: candidate.Slug!))
            .ToList();

        // A derived claim speaks only about a gap. It is computed from sibling file names, so it
        // agreeing with the file it was derived from is one source counted twice, not two agreeing.
        if (recognised.Any(candidate => !candidate.Claim.Source.IsDerived))
        {
            recognised = [.. recognised.Where(candidate => !candidate.Claim.Source.IsDerived)];
        }

        if (recognised.Count == 0)
        {
            // Something was written down and the list does not know it. That is not silence: it is
            // the value a person has to answer, or open a proposal for.
            return new FieldDecision { Field = TrackField.Dance, Reason = DecisionReason.Unusable };
        }

        var bySlug = recognised
            .GroupBy(candidate => candidate.Slug, StringComparer.Ordinal)
            .Select(group => (
                Slug: group.Key,
                Claims: group.Select(candidate => candidate.Claim).ToList(),
                Kinds: group.Select(candidate => candidate.Claim.Source.Kind).Distinct().Count()))
            .ToList();

        var mostKinds = bySlug.Max(entry => entry.Kinds);
        var leading = bySlug.Where(entry => entry.Kinds == mostKinds).ToList();

        if (leading.Count == 1)
        {
            return new FieldDecision
            {
                Field = TrackField.Dance,
                Value = leading[0].Slug,
                Reason = mostKinds > 1 ? DecisionReason.Corroborated : DecisionReason.SoleValue,
                Chosen = leading[0].Claims
            };
        }

        // A dance in brackets was written as a statement about the track; an ordinary word in a
        // title is an accident of language. "Tour" is a real dance and it must not tie with the
        // "(Mazurka)" somebody put there on purpose.
        var deliberate = leading.Where(entry => entry.Claims.Any(claim => claim.Source.IsDeliberate)).ToList();
        if (deliberate.Count == 1)
        {
            return new FieldDecision
            {
                Field = TrackField.Dance,
                Value = deliberate[0].Slug,
                Reason = DecisionReason.Deliberate,
                Chosen = deliberate[0].Claims
            };
        }

        return new FieldDecision { Field = TrackField.Dance, Reason = DecisionReason.Contested };
    }

    /// <summary>
    /// Decides a field whose sources are ordered rather than corroborating: the first usable claim
    /// answers.
    /// </summary>
    /// <remarks>
    /// Only the dance can be checked against a list, so artist and title cannot be told apart by
    /// agreement alone: an album artist and a performer disagreeing is ordinary, not a contest.
    /// What separates them is which source is trusted more, which is a thing the user declares and
    /// which the collector expresses as the order the claims come in.
    /// </remarks>
    private static FieldDecision DecideInOrder(TrackField field, List<Claim> claims)
    {
        if (claims.Count == 0)
        {
            return new FieldDecision { Field = field, Reason = DecisionReason.NoClaim };
        }

        // Dances are a closed set and get a whitelist; artists and titles are open sets and get a
        // blocklist instead. "Unknown Artist" is what a ripper writes when it knows nothing.
        var chosen = claims.FirstOrDefault(claim => !ArtistNames.IsPlaceholder(claim.Value));
        if (chosen is null)
        {
            return new FieldDecision { Field = field, Reason = DecisionReason.Unusable };
        }

        var folded = StringNormalizer.Normalize(chosen.Value);
        var agreeing = claims
            .Where(claim => string.Equals(StringNormalizer.Normalize(claim.Value), folded, StringComparison.Ordinal))
            .ToList();

        var kinds = agreeing.Select(claim => claim.Source.Kind).Distinct().Count();

        return new FieldDecision
        {
            Field = field,
            Value = chosen.Value,
            Reason = kinds > 1
                ? DecisionReason.Corroborated
                : claims.Count > 1
                    ? DecisionReason.Preferred
                    : DecisionReason.SoleValue,
            Chosen = agreeing
        };
    }

    /// <summary>
    /// The dance-shaped text to show, decided or not.
    /// </summary>
    /// <remarks>
    /// A recognised dance shows the list's own spelling, so 34 files spelled four ways group as one.
    /// An unrecognised one shows exactly what was written, because that is the thing a person has to
    /// map or propose.
    /// </remarks>
    private static string? DescribeDance(FieldDecision decision, List<Claim> claims, DanceListIndex index)
    {
        if (decision.Value is not null)
        {
            return index.DisplayNameFor(decision.Value);
        }

        var claim = claims.FirstOrDefault(candidate => candidate.Source.IsDeliberate) ?? claims.FirstOrDefault();
        if (claim is null)
        {
            return null;
        }

        return index.ResolveSlug(claim.Value) is { } slug ? index.DisplayNameFor(slug) : claim.Value;
    }
}
