using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Models.Settings;

/// <summary>Which tag fields may speak for which field of a track.</summary>
/// <remarks>
/// <para>
/// Null means the built-in default, which is a guess and is claimed as one. A list the user filled
/// in is a declaration: they are stating that this field holds that value in their library, so what
/// it says is claimed at the top tier and approved by the same act.
/// </para>
/// <para>
/// An empty list is not the same as null. It says "nothing in the tags speaks for this", which is a
/// real thing to declare about a library whose tags are all ripper defaults.
/// </para>
/// <para>
/// This is about a tag field being read <em>as</em> a value, verbatim. Finding a dance name from the
/// list inside ordinary text is a different mechanism, and it is not governed here: the vocabulary
/// recognising itself needs no permission.
/// </para>
/// </remarks>
public sealed record TagTrust
{
    /// <summary>What is used when nothing is declared. Claimed as observed, because it is a guess.</summary>
    public static readonly IReadOnlyList<TagField> DefaultForArtist = [TagField.AlbumArtist, TagField.Artist];

    public static readonly IReadOnlyList<TagField> DefaultForTitle = [TagField.Title];

    /// <summary>Nothing, because no tag field is reliably the dance. It is a thing to declare.</summary>
    public static readonly IReadOnlyList<TagField> DefaultForDance = [];

    public IReadOnlyList<TagField>? Artist { get; init; }

    public IReadOnlyList<TagField>? Title { get; init; }

    public IReadOnlyList<TagField>? Dance { get; init; }

    /// <summary>True when the user has stated something here, rather than the defaults applying.</summary>
    public bool IsDeclared => Artist is not null || Title is not null || Dance is not null;

    /// <summary>Compares what was declared rather than which list objects hold it.</summary>
    public bool Equals(TagTrust? other) =>
        other is not null
        && Same(Artist, other.Artist)
        && Same(Title, other.Title)
        && Same(Dance, other.Dance);

    public override int GetHashCode() => HashCode.Combine(Artist?.Count, Title?.Count, Dance?.Count);

    /// <summary>The fields that may speak for a track field, and whether the user said so.</summary>
    public (IReadOnlyList<TagField> Fields, bool Declared) For(TrackField field) => field switch
    {
        TrackField.Artist => (Artist ?? DefaultForArtist, Artist is not null),
        TrackField.Title => (Title ?? DefaultForTitle, Title is not null),
        _ => (Dance ?? DefaultForDance, Dance is not null)
    };

    private static bool Same(IReadOnlyList<TagField>? left, IReadOnlyList<TagField>? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);
}
