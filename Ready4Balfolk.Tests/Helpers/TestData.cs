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

    public static Dance CreateDance(string slug, int weight = 1, params string[] names)
        => new()
        {
            Slug = slug,
            Names = names.Length > 0 ? names : [slug],
            Weight = weight
        };

    public static DanceCategory CreateCategory(string name, int weight = 1,
        IEnumerable<Dance>? dances = null, IEnumerable<DanceCategory>? categories = null)
        => new()
        {
            Name = name,
            Weight = weight,
            Dances = dances?.ToList() ?? [],
            Categories = categories?.ToList() ?? []
        };

    /// <summary>
    /// Standard dance list:
    /// Common (weight 2)
    ///   ├─ mazurka [Mazurka, Mazurk] (weight 1)
    ///   └─ scottish [Scottish, Schottische] (weight 1)
    /// Bretagne (weight 1)
    ///   └─ Suite plinn (weight 3)
    ///       └─ plinn [Plinn] (weight 2)
    /// </summary>
    public static DanceList CreateSimpleDanceList()
        => new()
        {
            Categories =
            [
                CreateCategory("Common", 2, dances:
                [
                    CreateDance("mazurka", names: ["Mazurka", "Mazurk"]),
                    CreateDance("scottish", names: ["Scottish", "Schottische"])
                ]),
                CreateCategory("Bretagne", categories:
                [
                    CreateCategory("Suite plinn", 3, dances: [CreateDance("plinn", 2, "Plinn")])
                ])
            ]
        };
}
