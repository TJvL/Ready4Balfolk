using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class DanceListStoreTests : IDisposable
{
    private const string CacheFileName = "dance_list.json";

    private readonly IDirectoryInfo _tempDir;
    private readonly FileSystem _fileSystem;
    private readonly IDanceListFeed _feed = Substitute.For<IDanceListFeed>();
    private readonly DanceListStore _sut;

    public DanceListStoreTests()
    {
        _fileSystem = new FileSystem();
        _tempDir = _fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => _tempDir);
        _feed.HomePage.Returns(new Uri("https://example.invalid/list"));
        _sut = new DanceListStore(settingsDirectory, _fileSystem, _feed, new NoOpLoggerService(), TimeProvider.System);
    }

    [Fact]
    public async Task LoadAsync_NoCachedFile_HasNoListAtAll()
    {
        await _sut.LoadAsync(CancellationToken.None);

        // Nothing is shipped to fall back on: a machine nobody has fetched or imported on has no
        // vocabulary, and everything that needs one says so rather than guessing.
        Assert.True(_sut.Current.IsEmpty);
        Assert.Equal(DanceListOrigin.None, _sut.Status.Origin);
        Assert.Null(_sut.Status.ObtainedAt);
    }

    [Fact]
    public async Task LoadAsync_CachedFile_IsPreferred()
    {
        await WriteCache(TestData.CreateSimpleDanceList());

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(3, _sut.Current.Dances.Count);
        Assert.Equal(DanceListOrigin.Cached, _sut.Status.Origin);
        Assert.NotNull(_sut.Status.ObtainedAt);
    }

    [Fact]
    public async Task LoadAsync_BuildsTheIndexWithTheList()
    {
        await WriteCache(TestData.CreateSimpleDanceList());

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal("mazurka", _sut.Index.ResolveSlug("Mazurk"));
    }

    [Fact]
    public async Task LoadAsync_UnreadableCache_IsThrownAwayAndNothingStandsInItsPlace()
    {
        await File.WriteAllTextAsync(CachePath, "{ not json", TestContext.Current.CancellationToken);

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(DanceListOrigin.None, _sut.Status.Origin);
        // Nothing of the user's is in a cached copy of a published file, and nothing is left
        // behind for them to find later and wonder about.
        Assert.False(File.Exists(CachePath));
        Assert.Empty(_tempDir.GetFiles("*.bak"));
    }

    [Fact]
    public async Task LoadAsync_CacheInADeadFormat_IsThrownAway()
    {
        // Version 2 was the shape before the list became tags-only. There is no migration: the
        // file goes, and the machine is left with no list until one is fetched or imported.
        await File.WriteAllTextAsync(CachePath, """{"formatVersion":2,"categories":[]}""", TestContext.Current.CancellationToken);

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(DanceListOrigin.None, _sut.Status.Origin);
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task RefreshAsync_TakesThePublishedList()
    {
        await _sut.LoadAsync(CancellationToken.None);
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        var update = await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DanceListUpdateOutcome.Updated, update.Outcome);
        Assert.Equal(3, _sut.Current.Dances.Count);
        Assert.Equal(DanceListOrigin.Downloaded, _sut.Status.Origin);
    }

    [Fact]
    public async Task RefreshAsync_CachesWhatItTook()
    {
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(CachePath));
    }

    [Fact]
    public async Task RefreshAsync_SameListAgain_ReportsNoChange()
    {
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        await _sut.RefreshAsync(TestContext.Current.CancellationToken);
        var second = await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DanceListUpdateOutcome.AlreadyCurrent, second.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_Offline_KeepsTheListItHas()
    {
        await _sut.LoadAsync(CancellationToken.None);
        var before = _sut.Current;
        _feed.DownloadAsync(Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new HttpRequestException("no network"));

        var update = await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        // Offline is an ordinary state for a laptop in a hall, not a failure to recover from.
        Assert.Equal(DanceListUpdateOutcome.Failed, update.Outcome);
        Assert.Equal(before, _sut.Current);
    }

    [Fact]
    public async Task RefreshAsync_RefusedList_KeepsTheListItHas()
    {
        await _sut.LoadAsync(CancellationToken.None);
        var before = _sut.Current;
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns("""{"formatVersion":4,"dances":[]}""");

        var update = await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DanceListUpdateOutcome.Failed, update.Outcome);
        Assert.Equal(before, _sut.Current);
    }

    [Fact]
    public async Task UpdateFromFileAsync_TakesTheList()
    {
        var file = new FileInfo(Path.Combine(_tempDir.FullName, "carried_in.json"));
        await File.WriteAllTextAsync(file.FullName, Serialise(TestData.CreateSimpleDanceList()), TestContext.Current.CancellationToken);

        var update = await _sut.UpdateFromFileAsync(_fileSystem.FileInfo.New(file.FullName), TestContext.Current.CancellationToken);

        Assert.Equal(DanceListUpdateOutcome.Updated, update.Outcome);
        Assert.Equal(3, _sut.Current.Dances.Count);
        Assert.Equal(DanceListOrigin.File, _sut.Status.Origin);
    }

    [Fact]
    public async Task UpdateFromFileAsync_NameSharedByTwoDances_IsRefused()
    {
        var file = new FileInfo(Path.Combine(_tempDir.FullName, "broken.json"));
        await File.WriteAllTextAsync(file.FullName, Serialise(new DanceList
        {
            Dances =
            [
                TestData.CreateDance("hanter-dro", names: ["Hanter dro"]),
                TestData.CreateDance("andro", names: ["Hanter-dro"])
            ]
        }), TestContext.Current.CancellationToken);

        var update = await _sut.UpdateFromFileAsync(_fileSystem.FileInfo.New(file.FullName), TestContext.Current.CancellationToken);

        // An ambiguous name is exactly what would make discovery answer with a set of dances.
        Assert.Equal(DanceListUpdateOutcome.Failed, update.Outcome);
        Assert.Contains("Hanter-dro", update.Problem);
    }

    [Fact]
    public async Task RefreshAsync_LeavesNoTemporaryFileBehind()
    {
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        await _sut.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(CachePath + ".tmp"));
    }

    /// <summary>The cached list is never opened for writing at its real path.</summary>
    /// <remarks>
    /// This is the atomic write, stated as something a test can see. A plain write truncates before
    /// it writes, so a crash part way through left half a file, which the next start cannot read and
    /// throws away: in a hall with no network and nothing shipped to fall back on, that is the whole
    /// dance vocabulary gone. A crash mid-write cannot be staged, so what is asserted instead is the
    /// mechanism: the real path reaches Move and never reaches a write.
    /// </remarks>
    [Fact]
    public async Task RefreshAsync_TheCachedFileIsOnlyEverMovedIntoPlace()
    {
        var mock = new MockFileSystem();
        mock.Directory.CreateDirectory(_tempDir.FullName);

        var written = new List<string>();
        var moved = new List<string>();

        var file = Substitute.For<IFile>();
        file.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                written.Add(call.ArgAt<string>(0));
                return mock.File.WriteAllTextAsync(call.ArgAt<string>(0), call.ArgAt<string>(1), CancellationToken.None);
            });
        file.When(f => f.Move(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call =>
            {
                moved.Add(call.ArgAt<string>(1));
                mock.File.Move(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<bool>(2));
            });

        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Returns(file);
        fileSystem.FileInfo.Returns(mock.FileInfo);

        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => mock.DirectoryInfo.New(_tempDir.FullName));

        var feed = Substitute.For<IDanceListFeed>();
        feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        using var store = new DanceListStore(
            settingsDirectory, fileSystem, feed, new NoOpLoggerService(), TimeProvider.System);
        await store.RefreshAsync(TestContext.Current.CancellationToken);

        var expected = Path.Combine(mock.DirectoryInfo.New(_tempDir.FullName).FullName, CacheFileName);
        Assert.DoesNotContain(expected, written);
        Assert.Contains(expected + ".tmp", written);
        Assert.Contains(expected, moved);
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (_tempDir.Exists)
        {
            _tempDir.Delete(recursive: true);
        }
    }

    private string CachePath => Path.Combine(_tempDir.FullName, CacheFileName);

    private static string Serialise(DanceList list) => JsonSerializer.Serialize(list);

    private Task WriteCache(DanceList list) => File.WriteAllTextAsync(CachePath, Serialise(list), TestContext.Current.CancellationToken);
}
