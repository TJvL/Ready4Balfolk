using Ready4Balfolk.Domain.Services.Library;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Which folders a scan cannot speak for, which is the whole of when the application asks.
/// </summary>
/// <remarks>
/// A scan cannot tell a drive that has not mounted from a folder emptied on purpose. Everything
/// here is about being wrong in the safe direction: asking about a folder that is genuinely empty
/// costs a dialog, and not asking about one that is merely unreachable costs the approvals.
/// </remarks>
public sealed class MissingFoldersTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "music");

    private static string At(params string[] parts) => Path.Combine([Root, .. parts]);

    private static IReadOnlyList<MissingLibraryFolder> Detect(
        IEnumerable<string> indexed,
        IEnumerable<string>? withMusic = null,
        IReadOnlyDictionary<string, string>? unreadable = null) =>
        MissingFolders.Detect(
            indexed,
            new HashSet<string>(withMusic ?? [], StringComparer.Ordinal),
            unreadable ?? new Dictionary<string, string>(StringComparer.Ordinal),
            Root);

    /// <summary>Nothing is at risk, so there is nothing to ask about. A first run lands here.</summary>
    [Fact]
    public void AnEmptyIndex_AsksNothing() => Assert.Empty(Detect([]));

    [Fact]
    public void AFolderTheScanFoundMusicIn_IsNotMissing() =>
        Assert.Empty(Detect([At("nas", "a.mp3")], withMusic: [At("nas")]));

    /// <summary>
    /// The failure this exists for. Every row and every approval under the folder used to go.
    /// </summary>
    [Fact]
    public void AFolderWithIndexedTracksAndNoMusicFound_IsMissing()
    {
        var missing = Detect([At("nas", "a.mp3"), At("nas", "b.mp3")], withMusic: [At("local")]);

        var folder = Assert.Single(missing);
        Assert.Equal(At("nas"), folder.Path);
        Assert.Equal(2, folder.TrackCount);
        Assert.Null(folder.Error);
    }

    /// <summary>Speculating about the cause is exactly what a scan must not do.</summary>
    [Fact]
    public void AFolderThatWouldNotOpen_CarriesTheReasonVerbatim()
    {
        var missing = Detect(
            [At("nas", "a.mp3")],
            withMusic: [At("local")],
            unreadable: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [At("nas")] = "Permission denied"
            });

        Assert.Equal("Permission denied", Assert.Single(missing).Error);
    }

    /// <summary>
    /// One folder and one number, not four hundred lines. A walk that found music somewhere had to
    /// open every folder above it, so a subtree with nothing anywhere in it is one thing gone.
    /// </summary>
    [Fact]
    public void AWholeSubtreeGone_IsReportedAsItsTopmostFolder()
    {
        var missing = Detect(
            [At("nas", "mazurkas", "a.mp3"), At("nas", "bourrees", "b.mp3"), At("nas", "c.mp3")],
            withMusic: [At("local")]);

        var folder = Assert.Single(missing);
        Assert.Equal(At("nas"), folder.Path);
        Assert.Equal(3, folder.TrackCount);
    }

    /// <summary>The music directory itself, which is the mount point that did not mount.</summary>
    [Fact]
    public void NothingFoundAnywhere_IsReportedAsTheMusicDirectory()
    {
        var missing = Detect([At("nas", "a.mp3"), At("local", "b.mp3")]);

        var folder = Assert.Single(missing);
        Assert.Equal(Root, folder.Path);
        Assert.Equal(2, folder.TrackCount);
    }

    /// <summary>
    /// A folder that opened and holds a folder that gave up music cannot be a failed mount: those
    /// are empty all the way down. Files somebody deleted are ordinary housekeeping.
    /// </summary>
    [Fact]
    public void AFolderWhoseChildStillHoldsMusic_IsNotAskedAbout() =>
        Assert.Empty(Detect(
            [At("nas", "a.mp3"), At("nas", "mazurkas", "b.mp3")],
            withMusic: [At("nas", "mazurkas")]));

    /// <summary>Rows of a music directory the library has been pointed away from are not this scan's business.</summary>
    [Fact]
    public void RowsOutsideTheMusicDirectory_AreNotAskedAbout() =>
        Assert.Empty(Detect(
            [Path.Combine(Path.GetTempPath(), "elsewhere", "a.mp3")], withMusic: [Root]));

    [Fact]
    public void PathsIn_IsEverythingUnderTheReportedFolders()
    {
        var missing = Detect([At("nas", "mazurkas", "a.mp3"), At("local", "b.mp3")], withMusic: [At("local")]);

        Assert.Equal(
            [At("nas", "mazurkas", "a.mp3")],
            MissingFolders.PathsIn([At("nas", "mazurkas", "a.mp3"), At("local", "b.mp3")], missing));
    }
}
