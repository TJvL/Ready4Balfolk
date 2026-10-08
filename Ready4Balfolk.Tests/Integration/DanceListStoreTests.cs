using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
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
    private readonly RecordingLoggerService _logger = new();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly DanceListStore _sut;

    public DanceListStoreTests()
    {
        _fileSystem = new FileSystem();
        _tempDir = _fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => _tempDir);
        _feed.HomePage.Returns(new Uri("https://example.invalid/list"));
        _sut = new DanceListStore(settingsDirectory, _fileSystem, _feed, _logger, _notifications, TimeProvider.System);
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
    public async Task AFailedUpdate_SaysWhyInTheDjsLanguage_AndLeavesWhatDotNetSaidToTheLog()
    {
        // What the network stack and the file system have to say is in English, in their terms,
        // and used to be put inside the translated notice word for word. The DJ is told what did
        // not happen from the resx files; the exception's text is the log's.
        _feed.DownloadAsync(Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new HttpRequestException("No such host is known. (example.invalid:443)"));
        var offline = await _sut.RefreshAsync(TestContext.Current.CancellationToken);
        // A folder where the file should be, which refuses the read the way a locked file does.
        var unreadable = await _sut.UpdateFromFileAsync(
            _fileSystem.FileInfo.New(_tempDir.FullName), TestContext.Current.CancellationToken);

        Assert.Equal(DomainStrings.DanceList_Unreachable, offline.Problem);
        Assert.Equal(DomainStrings.DanceList_FileUnreadable, unreadable.Problem);
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
    public async Task ARefusedListOrAnUnreadableFile_IsHandedBackRatherThanPutOnScreenAsWell()
    {
        // The caller shows the failure in the DJ's language. An error from here as well was a
        // second notice for one refused file, in English.
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns("""{"formatVersion":4,"dances":[]}""");
        var refused = await _sut.RefreshAsync(TestContext.Current.CancellationToken);
        // A folder where the file should be, which refuses the read the way a locked file does.
        var unreadable = await _sut.UpdateFromFileAsync(
            _fileSystem.FileInfo.New(_tempDir.FullName), TestContext.Current.CancellationToken);

        Assert.Equal(DanceListUpdateOutcome.Failed, refused.Outcome);
        Assert.Equal(DanceListUpdateOutcome.Failed, unreadable.Outcome);
        Assert.Empty(_logger.Errors);
        _notifications.DidNotReceive().Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
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
            settingsDirectory, fileSystem, feed, new NoOpLoggerService(), Substitute.For<INotificationService>(),
            TimeProvider.System);
        await store.RefreshAsync(TestContext.Current.CancellationToken);

        var expected = Path.Combine(mock.DirectoryInfo.New(_tempDir.FullName).FullName, CacheFileName);
        Assert.DoesNotContain(expected, written);
        Assert.Contains(expected + ".tmp", written);
        Assert.Contains(expected, moved);
    }

    /// <summary>A refresh that lands while the cached copy is being read is not undone by it.</summary>
    /// <remarks>
    /// The read is held open after it has the older copy in hand, which is the window the load used
    /// to leave unguarded: a refresh finished inside it and the load then published what it had
    /// read over the list just adopted. The disk held the newer one, so the next start disagreed
    /// with everything this session had shown.
    /// </remarks>
    [Fact]
    public async Task LoadAsync_ARefreshDuringIt_KeepsTheNewerList()
    {
        var mock = new MockFileSystem();
        var directory = mock.DirectoryInfo.New(_tempDir.FullName);
        directory.Create();
        var cachePath = Path.Combine(directory.FullName, CacheFileName);
        var older = TestData.CreateSimpleDanceList();
        var newer = older with
        {
            Dances = [.. older.Dances, TestData.CreateDance("bourree", ["common"], "Bourree")]
        };
        await mock.File.WriteAllTextAsync(cachePath, Serialise(older), TestContext.Current.CancellationToken);

        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> ReadThenHold(string path)
        {
            var text = await mock.File.ReadAllTextAsync(path, CancellationToken.None);
            read.TrySetResult();
            await release.Task;
            return text;
        }

        var file = Substitute.For<IFile>();
        file.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ReadThenHold(call.ArgAt<string>(0)));
        file.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => mock.File.WriteAllTextAsync(
                call.ArgAt<string>(0), call.ArgAt<string>(1), CancellationToken.None));
        file.When(f => f.Move(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call => mock.File.Move(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<bool>(2)));

        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Returns(file);
        fileSystem.FileInfo.Returns(mock.FileInfo);

        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => mock.DirectoryInfo.New(_tempDir.FullName));

        var feed = Substitute.For<IDanceListFeed>();
        feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(newer));

        using var store = new DanceListStore(
            settingsDirectory, fileSystem, feed, new NoOpLoggerService(), Substitute.For<INotificationService>(),
            TimeProvider.System);

        var load = store.LoadAsync(TestContext.Current.CancellationToken);
        await read.Task;
        var refresh = store.RefreshAsync(TestContext.Current.CancellationToken);
        // Every chance for the refresh to finish while the load holds the older copy. Under the
        // gate it cannot, so how long this is decides only how reliably the old race shows.
        await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken));
        release.SetResult();
        await load;
        await refresh;

        Assert.Equal(4, store.Current.Dances.Count);
        Assert.Equal(DanceListOrigin.Downloaded, store.Status.Origin);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _logger.Dispose();
        if (_tempDir.Exists)
        {
            _tempDir.Delete(recursive: true);
        }
    }

    private string CachePath => Path.Combine(_tempDir.FullName, CacheFileName);

    private static string Serialise(DanceList list) => JsonSerializer.Serialize(list);

    private Task WriteCache(DanceList list) => File.WriteAllTextAsync(CachePath, Serialise(list), TestContext.Current.CancellationToken);
}
