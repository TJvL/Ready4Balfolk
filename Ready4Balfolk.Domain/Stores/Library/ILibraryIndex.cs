using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Library;

/// <summary>The index of what is in the music directory, so a startup can avoid opening files.</summary>
/// <summary>One field a person answered, for approving a row in a single transaction.</summary>
public readonly record struct FieldAnswer(TrackField Field, string Value);

public interface ILibraryIndex : IDisposable
{
    /// <summary>Opens the database and creates the schema if it is not there yet.</summary>
    Task OpenAsync(CancellationToken token = default);

    /// <summary>
    /// Every row, by path. Read once per scan: a scan asks about thousands of files, and answering
    /// each from memory is what keeps an unchanged startup from touching the disk at all.
    /// </summary>
    Task<IReadOnlyDictionary<string, LibraryEntry>> SnapshotByPathAsync(CancellationToken token = default);

    /// <summary>Inserts or updates rows, in one transaction.</summary>
    /// <remarks>
    /// Only ever derived values. What a person agreed to lives in its own table and is not touched
    /// by a scan, which is the whole reason the two are kept apart.
    /// </remarks>
    Task WriteAsync(IReadOnlyCollection<LibraryEntry> entries, CancellationToken token = default);

    /// <summary>
    /// What a person has agreed to, by content hash.
    /// </summary>
    /// <remarks>
    /// Keyed by <see cref="LibraryKey.For(byte[])"/> rather than by the bytes, because two equal
    /// hashes are two different arrays and a dictionary would not agree that they are the same
    /// track.
    /// </remarks>
    Task<IReadOnlyDictionary<string, IReadOnlyList<TrackApproval>>> ApprovalsAsync(CancellationToken token = default);

    /// <summary>Records what was agreed to, replacing any earlier answer for the same field.</summary>
    Task ApproveAsync(IReadOnlyCollection<TrackApproval> approvals, CancellationToken token = default);

    /// <summary>
    /// Takes back every approval that a rule gave, leaving the ones a person gave one at a time.
    /// </summary>
    /// <remarks>
    /// The user vouched for the rule rather than for each file it touched, so changing the rules has
    /// to undo their work. What somebody looked at and answered themselves is untouched.
    /// </remarks>
    Task RevokeRuleApprovalsAsync(CancellationToken token = default);

    /// <summary>Approves fields of the tracks at these paths, as a person deciding, in one write.</summary>
    Task ApproveIndividuallyAsync(
        IReadOnlyCollection<string> paths, IReadOnlyCollection<FieldAnswer> answers, CancellationToken token = default);

    /// <summary>Forgets every row whose path is not in the set, after a scan has been through.</summary>
    Task DeleteMissingAsync(IReadOnlyCollection<string> existingPaths, CancellationToken token = default);

    /// <summary>
    /// Forgets one path, for the watcher noticing a file go. An audio nothing points at any more is
    /// gone along with what was decided about it, exactly as a full scan would conclude.
    /// </summary>
    Task DeletePathAsync(string path, CancellationToken token = default);

    /// <summary>
    /// The values the user has said not to ask about again, folded for comparison.
    /// </summary>
    /// <remarks>
    /// Ignoring is a first-class answer, not a way of putting something off. Without it the badge
    /// sits at 137 forever, because a library always contains values that are genuinely not dances.
    /// </remarks>
    Task<IReadOnlySet<string>> GetIgnoredValuesAsync(CancellationToken token = default);

    Task IgnoreValueAsync(string value, CancellationToken token = default);

    Task StopIgnoringValueAsync(string value, CancellationToken token = default);

    /// <summary>How many files are waiting for a person rather than sitting in the library.</summary>
    /// <remarks>
    /// Over paths, because that is what the user sees. A track is in the library or in review and
    /// never both, so this is the number the review badge is for.
    /// </remarks>
    /// <summary>
    /// How many files the index knows, for a scan's progress line. Whether one is in review is the
    /// gate's decision, not a query: the published library reports that count itself.
    /// </summary>
    Task<int> CountIndexedAsync(CancellationToken token = default);

}
