using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
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

    /// <summary>Where it stands and why, which is what the row has to explain.</summary>
    public required TrackReview Review { get; init; }

    /// <summary>What answered each field, so a wrong source can be seen rather than guessed at.</summary>
    public required LibraryEntry Entry { get; init; }

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
public static class ReviewQueue
{
    /// <summary>Builds the queue from the index, what has been approved, and the published list.</summary>
    /// <param name="entries">Every row of the index, by path.</param>
    /// <param name="approvals">What a person has agreed to, by <see cref="LibraryKey"/>.</param>
    /// <param name="dances">The published list, the only vocabulary a dance may come from.</param>
    /// <param name="musicRoot">The music directory, so a folder can be named relative to it.</param>
    public static IReadOnlyList<ReviewGroup> Build(
        IReadOnlyDictionary<string, LibraryEntry> entries,
        IReadOnlyDictionary<string, IReadOnlyList<TrackApproval>> approvals,
        DanceListIndex dances,
        string musicRoot)
    {
        var waiting = new List<ReviewTrack>();

        foreach (var entry in entries.Values)
        {
            var forTrack = approvals.GetValueOrDefault(LibraryKey.For(entry.ContentHash), []);
            var review = ReviewGate.Evaluate(entry, forTrack, dances);

            if (review.IsInLibrary)
            {
                continue;
            }

            waiting.Add(new ReviewTrack
            {
                Path = entry.Path,
                ContentHash = entry.ContentHash,
                FileName = Path.GetFileName(entry.Path),
                Folder = FolderOf(entry.Path, musicRoot),
                Review = review,
                Entry = entry,
                Confidence = ConfidenceOf(entry, review)
            });
        }

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

    private static int Confidence(DerivedFrom from, ReviewedField field)
    {
        if (field.ApprovedAs is ApprovalKind.Individual)
        {
            return 5;
        }

        if (string.IsNullOrWhiteSpace(field.Value))
        {
            return 0;
        }

        return from.Reason switch
        {
            DecisionReason.NoClaim => 0,
            DecisionReason.Unusable or DecisionReason.Contested => 1,
            DecisionReason.SoleValue => 2,
            DecisionReason.Preferred or DecisionReason.Deliberate => 3,
            _ => 4
        };
    }

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
