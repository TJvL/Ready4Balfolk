using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Models.Tree;

namespace Ready4Balfolk.Tests.Helpers;

public static class TestData
{
    public static Track CreateTrack(string dance = "Mazurka", string artist = "Artist",
        string title = "Title", int lengthSeconds = 180, AudioFormat format = AudioFormat.Mp3)
        => new(dance, artist, title,
            new FileInfo($"/tmp/test/{dance}_{artist}_{title}.mp3".Replace(' ', '_')),
            TimeSpan.FromSeconds(lengthSeconds), format);

    public static DanceBranch CreateBranch(string name, int weight = 1,
        IEnumerable<DanceBranch>? children = null, IEnumerable<DanceLeaf>? leaves = null)
        => new()
        {
            Name = name,
            Weight = weight,
            Branches = children ?? [],
            Leafs = leaves ?? []
        };

    public static DanceLeaf CreateLeaf(string name, int weight = 1)
        => new(name, weight);

    public static DanceMainName CreateMainName(string name, params string[] synonyms)
        => new(name, synonyms.Select(s => new DanceSynonym(s)).ToList());

    /// <summary>
    /// Standard tree:
    /// Root
    ///   ├─ Folk (weight 2)
    ///   │   ├─ Mazurka (leaf, weight 1)
    ///   │   └─ Schottische (leaf, weight 1)
    ///   └─ Bal (weight 1)
    ///       ├─ Bourree (leaf, weight 1)
    ///       └─ Waltz (leaf, weight 2)
    /// </summary>
    public static IReadOnlyList<DanceBranch> CreateSimpleTree()
        =>
        [
            CreateBranch("Folk", 2, leaves:
            [
                CreateLeaf("Mazurka"),
                CreateLeaf("Schottische")
            ]),
            CreateBranch("Bal", leaves:
            [
                CreateLeaf("Bourree"),
                CreateLeaf("Waltz", 2)
            ])
        ];

    /// <summary>
    /// Standard synonyms:
    ///   Mazurka → [Mazurk, Mazou]
    ///   Scottisch → [Schottische, Reinlander]
    /// </summary>
    public static IReadOnlyList<DanceMainName> CreateSimpleSynonyms()
        =>
        [
            CreateMainName("Mazurka", "Mazurk", "Mazou"),
            CreateMainName("Scottisch", "Schottische", "Reinlander")
        ];

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
