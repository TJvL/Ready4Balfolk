using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Domain.Services.Library;

/// <summary>Why a track is not in the library, which a person has to be able to see.</summary>
public enum ReviewReason
{
    /// <summary>It is in the library. Nothing is waiting.</summary>
    None,

    /// <summary>A field has no value at all yet.</summary>
    Missing,

    /// <summary>Every field has a value and nobody has agreed to one of them.</summary>
    Unapproved,

    /// <summary>The dance is a value the published list does not know.</summary>
    UnknownDance,

    /// <summary>It was approved, and then the file changed underneath the approval.</summary>
    ChangedSinceApproval
}

/// <summary>What one field of a track reads as, and where that came from.</summary>
public sealed record ReviewedField
{
    public required TrackField Field { get; init; }

    /// <summary>The value: what was approved when something was, what was derived otherwise.</summary>
    public string? Value { get; init; }

    public ApprovalKind? ApprovedAs { get; init; }

    /// <summary>Which rule answered it, when a rule did.</summary>
    public string? Rule { get; init; }

    public bool IsApproved => ApprovedAs is not null;
}

/// <summary>Where one track stands: in the library, or waiting for a person, and why.</summary>
public sealed record TrackReview
{
    public required ReviewedField Dance { get; init; }

    public required ReviewedField Artist { get; init; }

    public required ReviewedField Title { get; init; }

    /// <summary>The dance as a slug, when the published list knows the value that was agreed to.</summary>
    public string? DanceSlug { get; init; }

    public required ReviewReason Reason { get; init; }

    public bool IsInLibrary => Reason is ReviewReason.None;

    public ReviewedField For(TrackField field) => field switch
    {
        TrackField.Dance => Dance,
        TrackField.Artist => Artist,
        _ => Title
    };
}

/// <summary>
/// The one way into the library: an artist, a title, a dance the published list knows, and somebody
/// having agreed to all three.
/// </summary>
/// <remarks>
/// <para>
/// A track is in the library or in review and never both, so this is a pure function of what is
/// stored rather than a flag anybody sets. Nothing can be half in: a value that reads as a lie
/// counts for nothing here, whatever the confidence behind it.
/// </para>
/// <para>
/// The dance is resolved against the list every time rather than being frozen when it was approved,
/// which is what makes a reimport sweep the parked tracks. A rule that answered "Rond de Landéda"
/// on twenty files keeps its approval while the list has never heard of it; the day a proposal is
/// merged and the list is imported, those twenty cross on their own and nobody is asked twice.
/// </para>
/// </remarks>
public static class ReviewGate
{
    /// <summary>Where a track stands, from what was derived, what was approved, and the list.</summary>
    /// <param name="entry">What the application worked out about the file.</param>
    /// <param name="approvals">What a person agreed to about it, if anything.</param>
    /// <param name="dances">The published list, which is the only vocabulary a dance may come from.</param>
    /// <param name="allowDancesOutsideTheList">
    /// Whether a dance the list does not carry may still reach the library. Off by default, because
    /// the shared list is what makes a name mean the same thing to everybody and a local answer is a
    /// proposal waiting to be made. On, the answer stands as the user gave it — and a random pick
    /// still cannot reach the track, since it draws by tag and a dance nobody has published has no
    /// tags to draw on.
    /// </param>
    public static TrackReview Evaluate(
        LibraryEntry entry,
        IReadOnlyList<TrackApproval> approvals,
        DanceListIndex dances,
        bool allowDancesOutsideTheList = false)
    {
        var dance = Field(TrackField.Dance, DerivedDance(entry, dances), approvals);
        var artist = Field(TrackField.Artist, entry.Artist, approvals);
        var title = Field(TrackField.Title, entry.Title, approvals);

        var slug = dance.Value is null ? null : SlugFor(dance.Value, dances);

        return new TrackReview
        {
            Dance = dance,
            Artist = artist,
            Title = title,
            DanceSlug = slug,
            Reason = Decide(entry, approvals, dance, artist, title, slug, allowDancesOutsideTheList)
        };
    }

    private static ReviewReason Decide(
        LibraryEntry entry,
        IReadOnlyList<TrackApproval> approvals,
        ReviewedField dance,
        ReviewedField artist,
        ReviewedField title,
        string? slug,
        bool allowDancesOutsideTheList)
    {
        ReviewedField[] fields = [dance, artist, title];

        if (fields.Any(field => string.IsNullOrWhiteSpace(field.Value)))
        {
            return ReviewReason.Missing;
        }

        if (fields.Any(field => !field.IsApproved))
        {
            return ReviewReason.Unapproved;
        }

        if (slug is null && !allowDancesOutsideTheList)
        {
            // Approved and still not in: the value is not a local problem to patch around, it is a
            // proposal at BigBalfolkList, and the track waits here until the list carries it.
            return ReviewReason.UnknownDance;
        }

        // The audio is the same, so the approval stands and the value is kept. Something about the
        // file changed under it though, and that is worth a person's eyes rather than a silent pass.
        return approvals.Any(approval => entry.LastWriteUtc > approval.FileWriteUtc)
            ? ReviewReason.ChangedSinceApproval
            : ReviewReason.None;
    }

    /// <summary>What was approved if anything was, and what was derived otherwise.</summary>
    private static ReviewedField Field(
        TrackField field, string? derived, IReadOnlyList<TrackApproval> approvals)
    {
        var approval = approvals.FirstOrDefault(candidate => candidate.Field == field);

        return new ReviewedField
        {
            Field = field,
            Value = approval?.Value ?? (string.IsNullOrWhiteSpace(derived) ? null : derived),
            ApprovedAs = approval?.Kind,
            Rule = approval?.Rule
        };
    }

    /// <summary>
    /// The dance the value stands for, or nothing when the published list has never heard of it.
    /// </summary>
    /// <remarks>
    /// A name or a slug: what a rule approved is whatever text it read, and what the tagging editor
    /// approved is the slug the user picked off the list. Both have to answer the same question.
    /// </remarks>
    private static string? SlugFor(string value, DanceListIndex dances) =>
        dances.ResolveSlug(value) ?? (dances.FindBySlug(value) is null ? null : value);

    /// <summary>
    /// The dance as text, because that is the currency an approval is in.
    /// </summary>
    /// <remarks>
    /// A recognised slug is shown as the list's own spelling so that four spellings read as one
    /// dance. An unrecognised claim keeps exactly what the file said, because that is the thing a
    /// person has to map or propose.
    /// </remarks>
    private static string? DerivedDance(LibraryEntry entry, DanceListIndex dances) =>
        entry.DanceSlug is { } slug ? dances.DisplayNameFor(slug) : entry.OriginalDance;
}
