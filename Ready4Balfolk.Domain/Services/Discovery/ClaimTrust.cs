namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>How far the thing that made a claim is trusted.</summary>
/// <remarks>
/// <para>
/// Three tiers and one currency. A tier is not a score to be averaged: the most trusted tier that
/// said anything about a field is the only one considered, because a user who declares a rule has
/// taken responsibility for it and code has no business hedging against them.
/// </para>
/// <para>
/// Ordered lowest first, so <c>&gt;</c> means "more trusted".
/// </para>
/// </remarks>
public enum ClaimTrust
{
    /// <summary>One file's own tags and file name. Lowest alone, two agreeing beats one.</summary>
    Observed,

    /// <summary>Calibration over the library's own strings. Always shown before it is used.</summary>
    Measured,

    /// <summary>An advanced discovery setting the user filled in. The user is stating the shape.</summary>
    Declared
}
