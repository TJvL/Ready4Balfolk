using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Why a field reads the way it does.</summary>
/// <remarks>
/// <para>
/// Kept because the review screen has to show where a value came from, and because "no answer" has
/// several different meanings that a person needs told apart: nobody said anything, everybody said
/// something useless, and two sources said different things are three separate situations.
/// </para>
/// <para>
/// The numbers are written into the library index and are the identity of the member, not its
/// position. They are pinned so a member added here can never be added in front of one: a scan
/// rebuilds an unchanged file's row out of the index rather than opening it again, so a shift
/// would have the review screen give a person the wrong grounds for a value, and no later scan
/// would put it back.
/// </para>
/// </remarks>
public enum DecisionReason
{
    /// <summary>No source offered anything at all.</summary>
    NoClaim = 0,

    /// <summary>Something was offered and none of it can be used: a placeholder, or a dance the list does not know.</summary>
    Unusable = 1,

    /// <summary>One value was offered, by one source.</summary>
    SoleValue = 2,

    /// <summary>Two independent sources offered the same value. The strongest thing available.</summary>
    Corroborated = 3,

    /// <summary>Several sources were ordered by how much they are trusted, and the first usable one answered.</summary>
    Preferred = 4,

    /// <summary>Several values, and exactly one of them was written on purpose.</summary>
    Deliberate = 5,

    /// <summary>Several values and nothing to separate them, so the honest answer is none.</summary>
    Contested = 6
}

/// <summary>What one field of a track was decided to be, and on what grounds.</summary>
public sealed record FieldDecision
{
    public required TrackField Field { get; init; }

    /// <summary>The answer, or null when there is none. Null is a real answer.</summary>
    /// <remarks>For <see cref="TrackField.Dance"/> this is a slug, because the list defines it.</remarks>
    public string? Value { get; init; }

    public required DecisionReason Reason { get; init; }

    /// <summary>The claims that carried it, so a person can see what spoke and disagree with it.</summary>
    public IReadOnlyList<Claim> Chosen { get; init; } = [];

    public bool IsDecided => Value is not null;

    public bool IsCorroborated => Reason is DecisionReason.Corroborated;
}
