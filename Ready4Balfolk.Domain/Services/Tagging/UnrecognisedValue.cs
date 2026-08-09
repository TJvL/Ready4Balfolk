namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>One distinct thing the library claims, that the dance list does not recognise.</summary>
/// <remarks>
/// The unit of work is the value, not the track. Twenty-one files claiming "Ar Re Yaouank" are one
/// decision, and presenting them as twenty-one is how a list of things to fix becomes a list nobody
/// ever finishes.
/// </remarks>
public sealed record UnrecognisedValue
{
    public required string Value { get; init; }

    public required UnrecognisedKind Kind { get; init; }

    /// <summary>Every track claiming this value, by file path.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    /// <summary>What it might mean, best first. Empty for a value nothing is near.</summary>
    public IReadOnlyList<DanceSuggestion> Suggestions { get; init; } = [];

    /// <summary>
    /// The album folders these tracks sit in, with what each folder already resolved to.
    /// </summary>
    /// <remarks>
    /// This is where the remaining evidence is for a value that is too general: a folder in which
    /// 8 of 11 tracks already read "Bourrée 3 temps" answers for the other three.
    /// </remarks>
    public IReadOnlyList<FolderBreakdown> Folders { get; init; } = [];

    public int TrackCount => Paths.Count;

    /// <summary>
    /// Whether one decision can settle every track at once.
    /// </summary>
    /// <remarks>
    /// False for a value that is too general, and the editor must offer no map button at all in that
    /// case rather than a disabled one: the point is that the question is wrong, not unavailable.
    /// </remarks>
    public bool CanMapAsAWhole => Kind is not (UnrecognisedKind.TooGeneral or UnrecognisedKind.Ambiguous);
}

/// <summary>One album folder holding tracks with an unrecognised value.</summary>
/// <param name="FolderKey">The folder, relative to the music directory.</param>
/// <param name="Paths">The unresolved tracks in it.</param>
/// <param name="Suggestions">
/// What the rest of that folder already resolved to, best first. A folder that gives nothing leaves
/// its tracks to be answered one at a time.
/// </param>
public sealed record FolderBreakdown(
    string FolderKey,
    IReadOnlyList<string> Paths,
    IReadOnlyList<DanceSuggestion> Suggestions);
