using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

/// <summary>The two ways a discovery makes a claim, so every one of them makes it the same way.</summary>
public static class ClaimCreator
{
    /// <summary>A dance somebody's text named, as the lowest tier.</summary>
    public static Claim Dance(string value, ClaimSource source) => new()
    {
        Field = TrackField.Dance,
        Value = value,
        Source = source,
        Trust = ClaimTrust.Observed
    };

    /// <summary>A claim of what was said, or nothing when nothing was.</summary>
    /// <remarks>
    /// Blank is not a value. A tag that is there and empty says as little as one that is missing,
    /// and claiming it would give a field an answer of nothing that outranks a real one below it.
    /// </remarks>
    public static Claim? AddIfSaid(TrackField field, string? value, ClaimSource source, ClaimTrust trust) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : new Claim
            {
                Field = field,
                Value = value.Trim(),
                Source = source,
                Trust = trust
            };
}
