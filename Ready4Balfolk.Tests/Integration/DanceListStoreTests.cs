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

    private readonly DirectoryInfo _tempDir;
    private readonly IDanceListFeed _feed = Substitute.For<IDanceListFeed>();
    private readonly DanceListStore _sut;

    public DanceListStoreTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => _tempDir);
        _feed.HomePage.Returns(new Uri("https://example.invalid/list"));
        _sut = new DanceListStore(settingsDirectory, _feed, new NoOpLoggerService());
    }

    [Fact]
    public async Task LoadAsync_NoCachedFile_UsesTheCopyShippedWithTheApplication()
    {
        await _sut.LoadAsync(CancellationToken.None);

        // A first run with no network still has a list, which is the whole point of shipping one.
        Assert.False(_sut.Current.IsEmpty);
        Assert.Equal(DanceListOrigin.BuiltIn, _sut.Status.Origin);
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
    public async Task LoadAsync_UnreadableCache_IsThrownAwayAndTheBuiltInCopyStands()
    {
        await File.WriteAllTextAsync(CachePath, "{ not json", TestContext.Current.CancellationToken);

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(DanceListOrigin.BuiltIn, _sut.Status.Origin);
        // Nothing of the user's is in a cached copy of a published file, and nothing is left
        // behind for them to find later and wonder about.
        Assert.False(File.Exists(CachePath));
        Assert.Empty(_tempDir.GetFiles("*.bak"));
    }

    [Fact]
    public async Task LoadAsync_CacheInADeadFormat_IsThrownAway()
    {
        // Version 2 was the shape before the list became tags-only. There is no migration: the
        // file goes and the published list takes over.
        await File.WriteAllTextAsync(CachePath, """{"formatVersion":2,"categories":[]}""", TestContext.Current.CancellationToken);

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(DanceListOrigin.BuiltIn, _sut.Status.Origin);
        Assert.False(File.Exists(CachePath));
    }

    [Fact]
    public async Task RefreshAsync_TakesThePublishedList()
    {
        await _sut.LoadAsync(CancellationToken.None);
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        var update = await _sut.RefreshAsync();

        Assert.Equal(DanceListUpdateOutcome.Updated, update.Outcome);
        Assert.Equal(3, _sut.Current.Dances.Count);
        Assert.Equal(DanceListOrigin.Downloaded, _sut.Status.Origin);
    }

    [Fact]
    public async Task RefreshAsync_CachesWhatItTook()
    {
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        await _sut.RefreshAsync();

        Assert.True(File.Exists(CachePath));
    }

    [Fact]
    public async Task RefreshAsync_SameListAgain_ReportsNoChange()
    {
        _feed.DownloadAsync(Arg.Any<CancellationToken>()).Returns(Serialise(TestData.CreateSimpleDanceList()));

        await _sut.RefreshAsync();
        var second = await _sut.RefreshAsync();

        Assert.Equal(DanceListUpdateOutcome.AlreadyCurrent, second.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_Offline_KeepsTheListItHas()
    {
        await _sut.LoadAsync(CancellationToken.None);
        var before = _sut.Current;
        _feed.DownloadAsync(Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new HttpRequestException("no network"));

        var update = await _sut.RefreshAsync();

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

        var update = await _sut.RefreshAsync();

        Assert.Equal(DanceListUpdateOutcome.Failed, update.Outcome);
        Assert.Equal(before, _sut.Current);
    }

    [Fact]
    public async Task UpdateFromFileAsync_TakesTheList()
    {
        var file = new FileInfo(Path.Combine(_tempDir.FullName, "carried_in.json"));
        await File.WriteAllTextAsync(file.FullName, Serialise(TestData.CreateSimpleDanceList()), TestContext.Current.CancellationToken);

        var update = await _sut.UpdateFromFileAsync(file);

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

        var update = await _sut.UpdateFromFileAsync(file);

        // An ambiguous name is exactly what would make discovery answer with a set of dances.
        Assert.Equal(DanceListUpdateOutcome.Failed, update.Outcome);
        Assert.Contains("Hanter-dro", update.Problem);
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
