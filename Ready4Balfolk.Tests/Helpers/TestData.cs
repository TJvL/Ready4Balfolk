using System.IO.Abstractions;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Models.Tree;

namespace Ready4Balfolk.Tests.Helpers;

public static class TestData
{
    public static Track CreateTrack(IFileSystem fileSystem, string dance = "Mazurka", string artist = "Artist",
        string title = "Title", int lengthSeconds = 180, AudioFormat format = AudioFormat.Mp3)
    {
        var filename = $"/tmp/test/{dance}_{artist}_{title}.mp3".Replace(' ', '_');

        var fileInfo = fileSystem.FileInfo.New(filename);
        fileInfo.Directory?.Create();
        using var stream = fileInfo.Create();

        return new(dance, artist, title, fileInfo,
            TimeSpan.FromSeconds(lengthSeconds), format);
    }

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
}
