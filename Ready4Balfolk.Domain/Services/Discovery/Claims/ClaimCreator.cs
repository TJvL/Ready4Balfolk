using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Services.Discovery.Claims;

public static class ClaimCreator
{
    public static Claim Dance(string value, ClaimSource source) => new()
    {
        Field = TrackField.Dance,
        Value = value,
        Source = source,
        Trust = ClaimTrust.Observed
    };

    public static Claim? AddIfSaid(TrackField field, string? value, ClaimSource source, ClaimTrust trust)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new Claim
        {
            Field = field,
            Value = value.Trim(),
            Source = source,
            Trust = trust
        };
    }
}
