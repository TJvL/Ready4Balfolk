using NSubstitute;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Library;

namespace Ready4Balfolk.Tests.Integration;

public sealed class SqliteLibraryIndexTests : IAsyncLifetime
{
    private readonly DirectoryInfo _tempDir;
    private readonly SqliteLibraryIndex _sut;

    public SqliteLibraryIndexTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => new(_sut.OpenAsync(Token));

    [Fact]
    public void Open_CreatesTheDatabaseFile()
        => Assert.True(File.Exists(Path.Combine(_tempDir.FullName, "library.sqlite")));

    [Fact]
    public async Task Write_ThenSnapshot_RoundTripsEverything()
    {
        var entry = Entry("/music/a.mp3", [1, 2, 3], slug: "mazurka");

        await _sut.WriteAsync([entry], Token);
        var snapshot = await _sut.SnapshotByPathAsync(Token);

        var read = snapshot["/music/a.mp3"];
        Assert.Equal(entry.ContentHash, read.ContentHash);
        Assert.Equal(entry.FileSize, read.FileSize);
        Assert.Equal(entry.LastWriteUtc, read.LastWriteUtc);
        Assert.Equal(entry.Duration, read.Duration);
        Assert.Equal(AudioFormat.Flac, read.Format);
        Assert.Equal("mazurka", read.DanceSlug);
        Assert.Equal("Mazurka", read.OriginalDance);
        Assert.Equal("Artist", read.Artist);
        Assert.Equal("Title", read.Title);
    }

    [Fact]
    public async Task Write_UnresolvedTrack_KeepsTheSlugNull()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: null)], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);

        Assert.Null(snapshot["/music/a.mp3"].DanceSlug);
    }

    [Fact]
    public async Task TheSameAudioInTwoPlaces_IsOneTrackKnownAtBothPaths()
    {
        // Real libraries hold the same album twice, loose and in a folder. Both copies have to be
        // known, or the forgotten one is read from disk on every single startup; and one decision
        // about the recording covers both, because it is the same recording.
        await _sut.WriteAsync([Entry("/music/loose.mp3", [9, 9], slug: "plinn")], Token);
        await _sut.WriteAsync([Entry("/music/album/track.mp3", [9, 9], slug: "plinn")], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("plinn", snapshot["/music/loose.mp3"].DanceSlug);
        Assert.Equal("plinn", snapshot["/music/album/track.mp3"].DanceSlug);
    }

    [Fact]
    public async Task AnsweringOneCopy_AnswersTheOther()
    {
        await _sut.WriteAsync([
            Entry("/music/loose.mp3", [9, 9], slug: null),
            Entry("/music/album/track.mp3", [9, 9], slug: null)
        ], Token);

        await _sut.AssignDanceAsync(["/music/loose.mp3"], "mazurka", Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Equal("mazurka", snapshot["/music/album/track.mp3"].DanceSlug);
    }

    [Fact]
    public async Task ARenamedFile_KeepsWhatWasDecidedAboutIt()
    {
        await _sut.WriteAsync([Entry("/music/old.mp3", [9, 9], slug: "plinn")], Token);

        await _sut.WriteAsync([Entry("/music/new.mp3", [9, 9], slug: "plinn")], Token);
        await _sut.DeleteMissingAsync(["/music/new.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Single(snapshot);
        Assert.Equal("plinn", snapshot["/music/new.mp3"].DanceSlug);
    }

    [Fact]
    public async Task DifferentAudio_GetsItsOwnRow()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        Assert.Equal(2, (await _sut.SnapshotByPathAsync(Token)).Count);
    }

    [Fact]
    public async Task DeleteMissing_ForgetsWhatIsNoLongerThere()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1]), Entry("/music/b.mp3", [2])], Token);

        await _sut.DeleteMissingAsync(["/music/a.mp3"], Token);

        var snapshot = await _sut.SnapshotByPathAsync(Token);
        Assert.Single(snapshot);
        Assert.True(snapshot.ContainsKey("/music/a.mp3"));
    }

    [Fact]
    public async Task DeleteMissing_WithNothingPresent_EmptiesTheIndex()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1])], Token);

        await _sut.DeleteMissingAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task CountUnresolved_CountsOnlyTheOnesWithNoDance()
    {
        await _sut.WriteAsync(
        [
            Entry("/music/a.mp3", [1], slug: "mazurka"),
            Entry("/music/b.mp3", [2], slug: null),
            Entry("/music/c.mp3", [3], slug: null)
        ], Token);

        Assert.Equal(2, await _sut.CountUnresolvedAsync(Token));
    }

    [Fact]
    public async Task CountUnresolved_SurvivesReopening()
    {
        await _sut.WriteAsync([Entry("/music/a.mp3", [1], slug: null)], Token);
        _sut.Dispose();

        using var reopened = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());
        await reopened.OpenAsync(Token);

        // The count is a query, so a restart costs nothing to keep it.
        Assert.Equal(1, await reopened.CountUnresolvedAsync(Token));
    }

    [Fact]
    public async Task Write_WithNothingToWrite_DoesNotThrow()
    {
        await _sut.WriteAsync([], Token);

        Assert.Empty(await _sut.SnapshotByPathAsync(Token));
    }

    [Fact]
    public async Task UseBeforeOpen_Throws()
    {
        using var unopened = new SqliteLibraryIndex(DirectoryPointingAtTemp(), new NoOpLoggerService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => unopened.SnapshotByPathAsync(Token));
    }

    public ValueTask DisposeAsync()
    {
        _sut.Dispose();
        if (_tempDir.Exists)
        {
            _tempDir.Delete(true);
        }

        return ValueTask.CompletedTask;
    }

    private IApplicationSettingsDirectory DirectoryPointingAtTemp()
    {
        var directory = Substitute.For<IApplicationSettingsDirectory>();
        directory.DirectoryInfoRoot.Returns(_ => _tempDir);
        return directory;
    }

    private static LibraryEntry Entry(string path, byte[] hash, string? slug = "mazurka")
        => new()
        {
            ContentHash = hash,
            Path = path,
            FileSize = 1234,
            LastWriteUtc = new DateTime(2026, 8, 8, 20, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromSeconds(180),
            Format = AudioFormat.Flac,
            DanceSlug = slug,
            OriginalDance = "Mazurka",
            Artist = "Artist",
            Title = "Title"
        };
}
