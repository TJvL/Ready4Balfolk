using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// What a folder says about the files in it that named no dance.
/// </summary>
/// <remarks>
/// Three private members of TrackStore until they were lifted out, and reachable before only by
/// driving a whole scan. The rules are small and worth stating on their own.
/// </remarks>
public sealed class FolderAgreementTests
{
    private const string Root = "/music";

    private static readonly MockFileSystem FileSystem = new();

    /// <summary>How the index remembers a dance that folder agreement gave.</summary>
    private static readonly DerivedFrom ByAgreement = new(
        ClaimSource.FolderAgreement.Kind, ClaimSource.FolderAgreement.Detail, DecisionReason.SoleValue);

    private readonly DanceListIndex _dances = DanceListIndex.Build(new DanceList
    {
        Dances =
        [
            TestData.CreateDance("mazurka", names: ["Mazurka"]),
            TestData.CreateDance("bourree", names: ["Bourrée"])
        ]
    });

    private static LibraryEntry Entry(string path, string? danceSlug) =>
        new()
        {
            ContentHash = [1],
            Path = path,
            FileSize = 1,
            LastWriteUtc = DateTime.UnixEpoch,
            Duration = TimeSpan.FromMinutes(3),
            Format = AudioFormat.Mp3,
            DanceSlug = danceSlug
        };

    // --- AgreedDance ---

    [Fact]
    public void AgreedDance_EveryVoiceTheSame_IsThatDance() =>
        Assert.Equal("mazurka", FolderAgreement.AgreedDance(["mazurka", "mazurka", "mazurka"]));

    /// <summary>A mixed folder is not evidence about the track that named none of them.</summary>
    [Fact]
    public void AgreedDance_AFolderOfSeveralDances_SaysNothing() =>
        Assert.Null(FolderAgreement.AgreedDance(["mazurka", "schottische"]));

    [Fact]
    public void AgreedDance_NobodyResolved_SaysNothing() =>
        Assert.Null(FolderAgreement.AgreedDance([]));

    /// <summary>One sibling that resolved is a folder that agrees with itself.</summary>
    [Fact]
    public void AgreedDance_ASingleVoice_IsEnough() =>
        Assert.Equal("bourree", FolderAgreement.AgreedDance(["bourree"]));

    // --- KeyFor ---

    [Fact]
    public void KeyFor_AFileDirectlyInTheRoot_HasTheEmptyKey() =>
        Assert.Equal(string.Empty, FolderAgreement.KeyFor(Path.Combine(Root, "a.mp3"), Root));

    /// <summary>
    /// Always forward slashes, so the key a scan built on Windows matches the one on Linux.
    /// </summary>
    [Fact]
    public void KeyFor_ASubfolder_IsItsRelativePath() =>
        Assert.Equal("Mazurkas", FolderAgreement.KeyFor(Path.Combine(Root, "Mazurkas", "a.mp3"), Root));

    [Fact]
    public void KeyFor_NestedFolders_KeepsTheWholePath() =>
        Assert.Equal("Trad/Mazurkas", FolderAgreement.KeyFor(Path.Combine(Root, "Trad", "Mazurkas", "a.mp3"), Root));

    /// <summary>Not under the root at all, so it belongs to no folder this scan knows about.</summary>
    [Fact]
    public void KeyFor_OutsideTheRoot_HasTheEmptyKey() =>
        Assert.Equal(string.Empty, FolderAgreement.KeyFor("/elsewhere/deep/a.mp3", Root));

    // --- Apply, the way the watcher calls it ---

    [Fact]
    public void Apply_SiblingsInTheSameFolderAgree_FillsTheDroppedInFile()
    {
        var droppedIn = Unnamed("Mazurkas", "new");

        var rescued = ApplyToDroppedIn(
            droppedIn,
            Entry(Path.Combine(Root, "Mazurkas", "one.mp3"), "mazurka"),
            Entry(Path.Combine(Root, "Mazurkas", "two.mp3"), "mazurka"));

        Assert.Equal(1, rescued);
        Assert.Equal("mazurka", droppedIn.Resolution.DanceSlug);
    }

    /// <summary>The folder around the file, not the library as a whole.</summary>
    [Fact]
    public void Apply_AnotherFolderAgrees_SaysNothing()
    {
        var droppedIn = Unnamed("Bourrees", "new");

        ApplyToDroppedIn(droppedIn, Entry(Path.Combine(Root, "Mazurkas", "one.mp3"), "mazurka"));

        Assert.Null(droppedIn.Resolution.DanceSlug);
    }

    /// <summary>
    /// The watcher reads a file again when it changes, while its old row is still in the index.
    /// Otherwise a file already carrying a dance would vote for its own answer, and the folder
    /// would always agree with whatever that file already said.
    /// </summary>
    [Fact]
    public void Apply_TheFileItself_IsNotAVoice()
    {
        var droppedIn = Unnamed("Mazurkas", "self");

        ApplyToDroppedIn(droppedIn, Entry(droppedIn.File.FullName, "mazurka"));

        Assert.Null(droppedIn.Resolution.DanceSlug);
    }

    /// <summary>
    /// A row whose file cannot be reached is kept, not consulted: the tracks on a drive that did
    /// not mount must not decide the dance of the ones that are still there.
    /// </summary>
    [Fact]
    public void Apply_ARowThatIsNotAvailable_IsNotAVoice()
    {
        var droppedIn = Unnamed("Mazurkas", "new");

        ApplyToDroppedIn(
            droppedIn,
            Entry(Path.Combine(Root, "Mazurkas", "one.mp3"), "mazurka") with { IsAvailable = false });

        Assert.Null(droppedIn.Resolution.DanceSlug);
    }

    [Fact]
    public void Apply_UnresolvedSiblings_AreNotVoices()
    {
        var droppedIn = Unnamed("Mixed", "new");

        ApplyToDroppedIn(
            droppedIn,
            Entry(Path.Combine(Root, "Mixed", "one.mp3"), null),
            Entry(Path.Combine(Root, "Mixed", "two.mp3"), "mazurka"));

        Assert.Equal("mazurka", droppedIn.Resolution.DanceSlug);
    }

    /// <summary>
    /// The scan filled 02 and 03 in from 01, and then 01 was deleted. What is left is the folder's
    /// own verdict about two files, and nothing in the folder says mazurka any more.
    /// </summary>
    [Fact]
    public void Apply_SiblingsTheFolderItselfAnswered_AreNotVoices()
    {
        var droppedIn = Unnamed("Mazurkas", "04 W");

        ApplyToDroppedIn(
            droppedIn,
            Entry(Path.Combine(Root, "Mazurkas", "02 Y.mp3"), "mazurka") with { Dance = ByAgreement },
            Entry(Path.Combine(Root, "Mazurkas", "03 Z.mp3"), "mazurka") with { Dance = ByAgreement });

        Assert.Null(droppedIn.Resolution.DanceSlug);
    }

    /// <summary>A file that names no dance, read the way the watcher reads one.</summary>
    private ScannedFile Unnamed(string folder, string name)
    {
        var evidence = TestData.CreateEvidence(name, segments: [folder]);

        return new ScannedFile(
            FileSystem.FileInfo.New(Path.Combine(Root, folder, evidence.FileName)),
            evidence,
            TrackInformationResolver.Resolve(evidence, _dances));
    }

    /// <summary>
    /// What the watcher hands over when one file is dropped into a folder the index already holds:
    /// a batch of that file alone, with every sibling answered from the index.
    /// </summary>
    private int ApplyToDroppedIn(ScannedFile droppedIn, params LibraryEntry[] known) =>
        FolderAgreement.Apply(
            [droppedIn],
            known.ToDictionary(entry => entry.Path, StringComparer.Ordinal),
            Root,
            _dances,
            DeclaredDiscovery.Undeclared);
}
