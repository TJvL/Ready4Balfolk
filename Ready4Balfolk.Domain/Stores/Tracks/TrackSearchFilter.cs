using Ready4Balfolk.Domain.Helpers;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Domain.Stores.Tracks;

/// <summary>The catalog's search box, as a predicate over a track.</summary>
/// <remarks>
/// Matches on the normalised form of every field a person might type, including the dance as it
/// was originally written, so searching for what is on the file finds it even after the list
/// renamed the dance.
/// </remarks>
public static class TrackSearchFilter
{

    public static Func<Track, bool> For(string search)
    {
        var normalized = StringNormalizer.Normalize(search);
        return string.IsNullOrEmpty(normalized)
            ? _ => true
            : track =>
                StringNormalizer.Normalize(track.Dance).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.OriginalDance).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.Artist).Contains(normalized, StringComparison.Ordinal) ||
                StringNormalizer.Normalize(track.Title).Contains(normalized, StringComparison.Ordinal);
    }
}
