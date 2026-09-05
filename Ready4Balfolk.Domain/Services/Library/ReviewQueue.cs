using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Services.Tagging;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Services.Library;

/// <summary>One track waiting for a person, with everything needed to answer it.</summary>
public sealed record ReviewTrack
{
    public required string Path { get; init; }

    public required byte[] ContentHash { get; init; }

    public required string FileName { get; init; }

    /// <summary>The folder it sits in, relative to the music directory. Empty at the root.</summary>
    public required string Folder { get; init; }

    /// <summary>
    /// Whether it actually sits in a folder, rather than loose in the music directory.
    /// </summary>
    /// <remarks>
    /// The root is not a grouping. Somebody filed these tracks together by putting them in a
    /// directory; the ones left lying in the music folder were filed nowhere, and treating that as
    /// a folder makes "answer this folder" mean "answer everything I never sorted".
    /// </remarks>
    public bool IsInFolder => Folder.Length > 0;

    /// <summary>Where it stands and why, which is what the row has to explain.</summary>
    public required TrackReview Review { get; init; }

    /// <summary>What answered each field, so a wrong source can be seen rather than guessed at.</summary>
    public required LibraryEntry Entry { get; init; }

    /// <summary>
    /// The dance value this track claims, when the published list does not know it.
    /// </summary>
    /// <remarks>
    /// Empty when the list knows it or when nothing was claimed. It is what makes answering twenty
    /// identical mistakes one decision instead of twenty, which is an optimisation on top of a queue
    /// over tracks rather than the shape of it.
    /// </remarks>
    public required string UnknownValue { get; init; }

    /// <summary>How many waiting tracks claim the same unknown value, this one included.</summary>
    public int SharedBy { get; init; }

    /// <summary>
    /// What the unknown value might have meant, best first.
    /// </summary>
    /// <remarks>
    /// Offered rather than applied: "Scottiche" is a misspelling of exactly one dance and saying so
    /// is a person's job, but making them type it out is a waste of the one thing this screen is
    /// short of. A value that fits several dances offers all of them, because that is the honest
    /// answer to "which bourrée is this".
    /// </remarks>
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    /// <summary>
    /// How sure the application is, lowest first.
    /// </summary>
    /// <remarks>
    /// The queue is ordered by this so that stopping early still leaves the library better: whoever
    /// gets through forty rows has answered the forty that nothing could speak for, not forty that
    /// two sources already agreed on.
    /// </remarks>
    public int Confidence { get; init; }
}

/// <summary>Tracks that sit together, because that is where the remaining evidence is.</summary>
public sealed record ReviewGroup
{
    /// <summary>The folder, or empty when the library has no grouping to offer.</summary>
    public required string Folder { get; init; }

    public required IReadOnlyList<ReviewTrack> Tracks { get; init; }

    /// <summary>How sure the application is about the least sure track in it.</summary>
    public int Confidence => Tracks.Count == 0 ? 0 : Tracks.Min(track => track.Confidence);

    /// <summary>False for the music directory itself, which is where the unfiled tracks are.</summary>
    public bool IsFolder => Folder.Length > 0;
}

/// <summary>
/// Everything waiting for a person, in the order that makes stopping early worth something.
/// </summary>
/// <remarks>
/// <para>
/// Over tracks, not over unrecognised values. A value-shaped queue can only hold the tracks that
/// said something wrong, and the 786 files in the reference library that say nothing at all have
/// nowhere to appear in one. Grouping identical mistakes into a single decision is an optimisation
/// on top of this, not the shape of it.
/// </para>
/// <para>
/// Grouped by whatever grouping the library actually has, which is folders, and flat when it has
/// none. A folder where eight of eleven tracks already read the same is the answer to the other
/// three, and it is the unit a person can confirm in one keystroke.
/// </para>
/// </remarks>
public static class ReviewQueueBuilder
{
    /// <summary>Builds the queue from the index, what has been approved, and the published list.</summary>
    /// <param name="entries">Every row of the index, by path.</param>
    /// <param name="approvals">What a person has agreed to, by <see cref="LibraryKey"/>.</param>
    /// <param name="dances">The published list, the only vocabulary a dance may come from.</param>
    /// <param name="musicRoot">The music directory, so a folder can be named relative to it.</param>
    /// <param name="ignored">
    /// Values the user has said are not dances, folded. A value in here is junk rather than an
    /// answer, so it is shown as nothing at all: leaving "trad" sitting in the dance box is leaving
    /// a wrong answer where a person is looking for a missing one.
    /// </param>
    /// <param name="allowDancesOutsideTheList">
    /// Whether a dance the published list does not carry may still reach the library, which is what
    /// decides whether such a track is still waiting here.
    /// </param>
    public static IReadOnlyList<ReviewGroup> Build(
        IReadOnlyDictionary<string, LibraryEntry> entries,
        IReadOnlyDictionary<string, IReadOnlyList<TrackApproval>> approvals,
        DanceListIndex dances,
        string musicRoot,
        IReadOnlySet<string>? ignored = null,
        bool allowDancesOutsideTheList = false)
    {
        var waiting = new List<ReviewTrack>();

        foreach (var entry in entries.Values)
        {
            if (!entry.IsAvailable)
            {
                // Kept, but not reachable. Nothing can be answered about a file that is not there:
                // the preview will not play and the tags cannot be written.
                continue;
            }

            var forTrack = approvals.GetValueOrDefault(LibraryKey.For(entry.ContentHash), []);
            var review = ReviewGate.Evaluate(entry, forTrack, dances, allowDancesOutsideTheList);

            if (review.IsInLibrary)
            {
                continue;
            }

            review = WithoutJunk(review, ignored);

            waiting.Add(new ReviewTrack
            {
                Path = entry.Path,
                ContentHash = entry.ContentHash,
                FileName = Path.GetFileName(entry.Path),
                Folder = FolderOf(entry.Path, musicRoot),
                Review = review,
                Entry = entry,
                UnknownValue = UnknownValueOf(review, dances),
                Suggestions = SuggestionsFor(UnknownValueOf(review, dances), dances),
                Confidence = ConfidenceOf(entry, review)
            });
        }

        // Counted over the queue rather than over the library: what matters is how many of the
        // tracks still waiting would be answered by one decision. A value that folds to nothing
        // ("???") is grouped with nothing: an empty key would lump it with every row that has no
        // value at all, and one decision must never answer those.
        var sharedBy = waiting
            .Where(track => StringNormalizer.Normalize(track.UnknownValue).Length > 0)
            .GroupBy(track => StringNormalizer.Normalize(track.UnknownValue), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        waiting =
        [
            .. waiting.Select(track =>
                sharedBy.TryGetValue(StringNormalizer.Normalize(track.UnknownValue), out var count)
                    ? track with { SharedBy = count }
                    : track)
        ];

        return
        [
            .. waiting
                .GroupBy(track => track.Folder, StringComparer.Ordinal)
                .Select(group => new ReviewGroup
                {
                    Folder = group.Key,
                    Tracks = [.. group.OrderBy(track => track.Confidence).ThenBy(track => track.FileName, StringComparer.Ordinal)]
                })
                .OrderBy(group => group.Confidence)
                .ThenBy(group => group.Folder, StringComparer.Ordinal)
        ];
    }

    /// <summary>How sure the application is about a track, as the sum of its three fields.</summary>
    /// <remarks>
    /// Nothing said at all is worth the least attention to fix and the most to ask about. Two
    /// independent sources agreeing is worth the most, and a rule the user declared is worth more
    /// still, because they have already vouched for it once.
    /// </remarks>
    private static int ConfidenceOf(LibraryEntry entry, TrackReview review) =>
        AllFields.Sum(field => Confidence(entry.From(field), review.For(field)));

    private static int Confidence(DerivedFrom from, ReviewedField field) => true switch
    {
        // Somebody looked at it and said yes, which nothing derived can equal.
        _ when field.ApprovedAs is ApprovalKind.Individual => 5,
        _ when string.IsNullOrWhiteSpace(field.Value) => 0,
        _ => from.Reason switch
        {
            DecisionReason.NoClaim => 0,
            DecisionReason.Unusable or DecisionReason.Contested => 1,
            DecisionReason.SoleValue => 2,
            DecisionReason.Preferred or DecisionReason.Deliberate => 3,
            _ => 4
        }
    };

    /// <summary>What an unrecognised value might have meant, in the list's own spelling.</summary>
    private static IReadOnlyList<string> SuggestionsFor(string value, DanceListIndex dances)
    {
        if (value.Length == 0)
        {
            return [];
        }

        var (_, slugs) = UnrecognisedValueClassifier.Classify(value, dances);

        return [.. slugs.Take(3).Select(dances.DisplayNameFor)];
    }

    /// <summary>The dance value this track claims that the list cannot answer, or nothing.</summary>
    private static string UnknownValueOf(TrackReview review, DanceListIndex dances) =>
        review.DanceSlug is null && review.Dance.Value is { } value && dances.ResolveSlug(value) is null
            ? value
            : string.Empty;

    /// <summary>
    /// Blanks a dance value the user has said is not a dance.
    /// </summary>
    /// <remarks>
    /// "trad" is not an answer, it is junk that came off a file, and leaving it in the box is
    /// leaving a wrong answer where somebody is looking for a missing one. Said once, it stays said:
    /// a rescan derives it again and it is blanked again.
    /// </remarks>
    private static TrackReview WithoutJunk(TrackReview review, IReadOnlySet<string>? ignored) =>
        ignored is null || review.Dance.Value is not { } value || review.DanceSlug is not null
            ? review
            : ignored.Contains(StringNormalizer.Normalize(value))
                ? review with { Dance = review.Dance with { Value = null }, Reason = ReviewReason.Missing }
                : review;

    private static string FolderOf(string path, string musicRoot)
    {
        if (string.IsNullOrWhiteSpace(musicRoot) || Path.GetDirectoryName(path) is not { } directory)
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(musicRoot, directory);

        return relative is "." || relative.StartsWith("..", StringComparison.Ordinal) ? string.Empty : relative;
    }

    private static readonly TrackField[] AllFields = [TrackField.Dance, TrackField.Artist, TrackField.Title];
}
