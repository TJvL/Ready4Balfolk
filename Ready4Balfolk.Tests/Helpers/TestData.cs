using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;

namespace Ready4Balfolk.Tests.Helpers;

public static class TestData
{
    /// <summary>
    /// A track. <paramref name="slug"/> defaults to the dance name lowercased, which matches the
    /// slugs in <see cref="CreateSimpleDanceList"/>; pass null for a track the list does not know.
    /// </summary>
    public static Track CreateTrack(string dance = "Mazurka", string artist = "Artist",
        string title = "Title", int lengthSeconds = 180, AudioFormat format = AudioFormat.Mp3,
        string? slug = "")
        => new(dance, artist, title,
            new FileInfo($"/tmp/test/{dance}_{artist}_{title}.mp3".Replace(' ', '_')),
            TimeSpan.FromSeconds(lengthSeconds), format)
        {
            DanceSlug = slug == string.Empty ? dance.ToLowerInvariant() : slug
        };

    public static Dance CreateDance(string slug, string[]? tags = null, params string[] names)
        => new()
        {
            Slug = slug,
            Names = names.Length > 0 ? names : [slug],
            Tags = tags ?? []
        };

    /// <summary>
    /// Standard dance list, in the shape BigBalfolkList publishes:
    /// mazurka [Mazurka, Mazurk]      common
    /// scottish [Scottish, Schottische] common
    /// plinn [Plinn]                  bretagne, suite
    /// </summary>
    public static DanceList CreateSimpleDanceList()
        => new()
        {
            Tags = ["bretagne", "common", "suite"],
            Dances =
            [
                CreateDance("mazurka", ["common"], "Mazurka", "Mazurk"),
                CreateDance("scottish", ["common"], "Scottish", "Schottische"),
                CreateDance("plinn", ["bretagne", "suite"], "Plinn")
            ]
        };
}
