using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Integration;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>What a batch of watcher reports is worth, and what it asks of the index.</summary>
/// <remarks>
/// Part of TrackStore until it was lifted out, and reachable before only by raising watcher events at
/// a whole store. The rules about a delete and a move meeting in one window are worth stating on
/// their own. In the audio collection because which extensions count as audio is one per process.
/// </remarks>
[Collection(ProcessWideAudioState.Name)]
public sealed class WatchedBatchTests
{
    private static readonly MockFileSystem FileSystem = new();

    public WatchedBatchTests()
    {
        SupportedAudioFormats.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3" });
    }

    private static LibraryFileChange Appeared(string path) => new(LibraryFileChangeKind.Appeared, path);

    private static LibraryFileChange Vanished(string path) => new(LibraryFileChangeKind.Vanished, path);

    private static LibraryFileChange Renamed(string from, string to) => new(LibraryFileChangeKind.Renamed, to, from);

    private static WatchedBatchPlan Plan(IEnumerable<string> indexed, params LibraryFileChange[] changes) =>
        WatchedBatchPlan.For(WatchedBatch.From(changes), indexed, FileSystem.Path);

    // --- WatchedBatch ---

    [Fact]
    public void AFileThatIsNotAudio_IsNothingToTheLibrary()
    {
        var batch = WatchedBatch.From([Appeared("/music/cover.jpg")]);

        Assert.True(batch.IsEmpty);
    }

    /// <summary>A folder going is one event, so anything that is not audio is taken for a folder.</summary>
    [Fact]
    public void Vanished_AFileIsGoneAndAnythingElseIsAFolder()
    {
        var batch = WatchedBatch.From([Vanished("/music/a.mp3"), Vanished("/music/album")]);

        Assert.Equal(["/music/a.mp3"], batch.Gone);
        Assert.Equal(["/music/album"], batch.GoneFolders);
    }

    [Fact]
    public void Renamed_AudioToAudio_IsReadAgainInThePlaceOfTheOld()
    {
        var batch = WatchedBatch.From([Renamed("/music/a.mp3", "/music/b.mp3")]);

        Assert.Equal([("/music/b.mp3", (string?)"/music/a.mp3")], batch.ToRead);
    }

    [Fact]
    public void Renamed_OutOfTheAudioFormats_IsTheFileGoing()
    {
        var batch = WatchedBatch.From([Renamed("/music/a.mp3", "/music/a.bak")]);

        Assert.Equal(["/music/a.mp3"], batch.Gone);
        Assert.Empty(batch.ToRead);
    }

    [Fact]
    public void Renamed_NeitherEndAudio_IsAFolderMoving()
    {
        var batch = WatchedBatch.From([Renamed("/music/old", "/music/new")]);

        Assert.Equal([("/music/old", "/music/new")], batch.Moved);
    }

    // --- WatchedBatchPlan ---

    /// <summary>A folder whose name another one starts with is not under it.</summary>
    [Fact]
    public void AFolderThatWentAway_TakesOnlyWhatIsUnderIt()
    {
        var plan = Plan(["/music/album/a.mp3", "/music/albums/b.mp3"], Vanished("/music/album"));

        Assert.Equal(["/music/album/a.mp3"], plan.Vanished);
        Assert.Equal(["/music/album/a.mp3"], plan.Forget);
    }

    [Fact]
    public void AFolderThatMoved_RepointsItsRowsAndSaysSo()
    {
        var plan = Plan(["/music/old/a.mp3"], Renamed("/music/old", "/music/new"));

        PathMove[] expected = [new("/music/old/a.mp3", "/music/new/a.mp3")];
        Assert.Equal(expected, plan.Repoint);
        Assert.Equal(expected, plan.Moved);
        Assert.Empty(plan.Forget);
    }

    /// <summary>A file deleted in the window its folder moved in is forgotten, not carried along.</summary>
    [Fact]
    public void AFileDeletedWhileItsFolderMoved_IsForgottenRatherThanRepointed()
    {
        var plan = Plan(
            ["/music/old/a.mp3", "/music/old/b.mp3"],
            Vanished("/music/old/a.mp3"),
            Renamed("/music/old", "/music/new"));

        Assert.Equal(["/music/old/a.mp3"], plan.Forget);
        Assert.Equal([new PathMove("/music/old/b.mp3", "/music/new/b.mp3")], plan.Repoint);
    }

    /// <summary>
    /// A file renamed on its own is read again rather than repointed, but whatever holds its old path
    /// is still told where it went, after any folder that moved.
    /// </summary>
    [Fact]
    public void AFileRenamedOnItsOwn_IsForgottenAtItsOldPathAndAnnouncedAsMoved()
    {
        var plan = Plan(
            ["/music/a.mp3", "/music/old/c.mp3"],
            Renamed("/music/a.mp3", "/music/b.mp3"),
            Renamed("/music/old", "/music/new"));

        Assert.Equal(["/music/a.mp3"], plan.Forget);
        Assert.Equal([new PathMove("/music/old/c.mp3", "/music/new/c.mp3")], plan.Repoint);
        Assert.Equal(
            [new PathMove("/music/old/c.mp3", "/music/new/c.mp3"), new PathMove("/music/a.mp3", "/music/b.mp3")],
            plan.Moved);
    }

    /// <summary>A file renamed into the audio formats was never a track, so nothing moved.</summary>
    [Fact]
    public void AFileRenamedIntoTheAudioFormats_ForgetsTheOldPathButMovesNothing()
    {
        var plan = Plan([], Renamed("/music/a.part", "/music/a.mp3"));

        Assert.Equal(["/music/a.part"], plan.Forget);
        Assert.Empty(plan.Moved);
    }
}
