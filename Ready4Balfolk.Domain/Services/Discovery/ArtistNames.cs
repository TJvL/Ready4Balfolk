using Ready4Balfolk.Domain.Helpers;

namespace Ready4Balfolk.Domain.Services.Discovery;

/// <summary>Decides whether a string is worth believing as an artist.</summary>
/// <remarks>
/// Dances are a closed set and get a whitelist: the dance list says what exists. Artists are an open
/// set and cannot, so they get a blocklist instead. Much of this music is personally ripped from CDs
/// that are in no database, and what a ripper writes when it knows nothing is exactly what must not
/// end up displayed as the artist.
/// </remarks>
public static class ArtistNames
{
    private static readonly HashSet<string> RipperDefaults = new(StringComparer.Ordinal)
    {
        "unknown artist",
        "unknown",
        "various artists",
        "various",
        "va",
        "no artist",
        "artist",
        "untitled",
        "track"
    };

    /// <summary>True when the value says nothing: blank, a ripper's placeholder, or only digits.</summary>
    public static bool IsPlaceholder(string? value)
    {
        var folded = StringNormalizer.Normalize(value ?? string.Empty);
        if (folded.Length == 0)
        {
            return true;
        }

        // Digits only: a track number that ended up in the artist field, which no ripper meant as a
        // name and which would sort every such file together under "07".
        return folded.All(c => char.IsDigit(c) || c == ' ') || RipperDefaults.Contains(folded);
    }

    /// <summary>The first value that is worth believing, or null when none of them are.</summary>
    public static string? FirstUsable(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !IsPlaceholder(candidate))?.Trim();
}
