using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Discovery;
using Ready4Balfolk.Domain.Stores.Library;

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

    // --- AgreedDanceAround ---

    [Fact]
    public void AgreedDanceAround_SiblingsInTheSameFolderAgree_IsThatDance()
    {
        var known = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [Path.Combine(Root, "Mazurkas", "one.mp3")] = Entry(Path.Combine(Root, "Mazurkas", "one.mp3"), "mazurka"),
            [Path.Combine(Root, "Mazurkas", "two.mp3")] = Entry(Path.Combine(Root, "Mazurkas", "two.mp3"), "mazurka")
        };

        var agreed = FolderAgreement.AgreedDanceAround(
            Path.Combine(Root, "Mazurkas", "new.mp3"), "Mazurkas", known, Root);

        Assert.Equal("mazurka", agreed);
    }

    /// <summary>The folder around the file, not the library as a whole.</summary>
    [Fact]
    public void AgreedDanceAround_AnotherFolderAgrees_SaysNothing()
    {
        var known = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [Path.Combine(Root, "Mazurkas", "one.mp3")] = Entry(Path.Combine(Root, "Mazurkas", "one.mp3"), "mazurka")
        };

        var agreed = FolderAgreement.AgreedDanceAround(
            Path.Combine(Root, "Bourrees", "new.mp3"), "Bourrees", known, Root);

        Assert.Null(agreed);
    }

    /// <summary>
    /// Otherwise a file already carrying a dance would vote for its own answer, and the folder
    /// would always agree with whatever that file already said.
    /// </summary>
    [Fact]
    public void AgreedDanceAround_TheFileItself_IsNotAVoice()
    {
        var path = Path.Combine(Root, "Mazurkas", "self.mp3");
        var known = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [path] = Entry(path, "mazurka")
        };

        Assert.Null(FolderAgreement.AgreedDanceAround(path, "Mazurkas", known, Root));
    }

    /// <summary>
    /// A row whose file cannot be reached is kept, not consulted: the tracks on a drive that did
    /// not mount must not decide the dance of the ones that are still there.
    /// </summary>
    [Fact]
    public void AgreedDanceAround_ARowThatIsNotAvailable_IsNotAVoice()
    {
        var sibling = Path.Combine(Root, "Mazurkas", "one.mp3");
        var known = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [sibling] = Entry(sibling, "mazurka") with { IsAvailable = false }
        };

        Assert.Null(FolderAgreement.AgreedDanceAround(
            Path.Combine(Root, "Mazurkas", "new.mp3"), "Mazurkas", known, Root));
    }

    [Fact]
    public void AgreedDanceAround_UnresolvedSiblings_AreNotVoices()
    {
        var known = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [Path.Combine(Root, "Mixed", "one.mp3")] = Entry(Path.Combine(Root, "Mixed", "one.mp3"), null),
            [Path.Combine(Root, "Mixed", "two.mp3")] = Entry(Path.Combine(Root, "Mixed", "two.mp3"), "mazurka")
        };

        var agreed = FolderAgreement.AgreedDanceAround(
            Path.Combine(Root, "Mixed", "new.mp3"), "Mixed", known, Root);

        Assert.Equal("mazurka", agreed);
    }
}
