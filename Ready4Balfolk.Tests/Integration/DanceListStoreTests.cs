using System.Reactive.Linq;
using System.Text.Json;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class DanceListStoreTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DirectoryInfo _tempDir;
    private readonly DanceListStore _sut;

    public DanceListStoreTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        var settingsDirectory = Substitute.For<IApplicationSettingsDirectory>();
        settingsDirectory.DirectoryInfoRoot.Returns(_ => _tempDir);
        _sut = new DanceListStore(settingsDirectory, new NoOpLoggerService());
    }

    [Fact]
    public async Task LoadAsync_NoFile_StaysEmpty()
    {
        await _sut.LoadAsync(CancellationToken.None);

        Assert.True(_sut.Current.IsEmpty);
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_RestoresTheList()
    {
        await WriteListFile(TestData.CreateSimpleDanceList());

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal(2, _sut.Current.Categories.Count);
        Assert.Equal(3, _sut.Current.AllDances.Count());
    }

    [Fact]
    public async Task LoadAsync_AlsoBuildsTheIndex()
    {
        await WriteListFile(TestData.CreateSimpleDanceList());

        await _sut.LoadAsync(CancellationToken.None);

        Assert.Equal("mazurka", _sut.Index.ResolveSlug("Mazurk"));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_LeavesTheStoreUsable()
    {
        await File.WriteAllTextAsync(ListFilePath, "{ not json", TestContext.Current.CancellationToken);

        await _sut.LoadAsync(CancellationToken.None);

        Assert.True(_sut.Current.IsEmpty);
    }

    [Fact]
    public async Task ReplaceAsync_PublishesAndPersists()
    {
        await _sut.ReplaceAsync(TestData.CreateSimpleDanceList());

        Assert.Equal(3, _sut.Current.AllDances.Count());
        Assert.True(File.Exists(ListFilePath));
    }

    [Fact]
    public async Task Index_FollowsEveryUpdate()
    {
        await _sut.ReplaceAsync(TestData.CreateSimpleDanceList());
        Assert.Equal("plinn", _sut.Index.ResolveSlug("Plinn"));

        await _sut.ReplaceAsync(DanceList.Empty);

        Assert.Null(_sut.Index.ResolveSlug("Plinn"));
    }

    [Fact]
    public async Task Observe_SeesTheIndexAlreadyRebuilt()
    {
        string? slugSeenBySubscriber = null;
        using var subscription = _sut.Observe().Skip(1).Subscribe(_ => slugSeenBySubscriber = _sut.Index.ResolveSlug("Mazurka"));

        await _sut.ReplaceAsync(TestData.CreateSimpleDanceList());

        Assert.Equal("mazurka", slugSeenBySubscriber);
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips()
    {
        await _sut.ReplaceAsync(TestData.CreateSimpleDanceList());
        var exported = new FileInfo(Path.Combine(_tempDir.FullName, "exported.json"));
        await _sut.ExportAsync(exported);

        await _sut.ReplaceAsync(DanceList.Empty);
        await _sut.ImportAsync(exported);

        Assert.Equal(3, _sut.Current.AllDances.Count());
        Assert.Equal("scottish", _sut.Index.ResolveSlug("Schottische"));
    }

    [Fact]
    public async Task ImportAsync_MissingFile_Throws()
    {
        var missing = new FileInfo(Path.Combine(_tempDir.FullName, "nope.json"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => _sut.ImportAsync(missing));
    }

    [Fact]
    public async Task ImportAsync_NameClaimedByTwoDances_IsRefusedAndChangesNothing()
    {
        await _sut.ReplaceAsync(TestData.CreateSimpleDanceList());
        var broken = new FileInfo(Path.Combine(_tempDir.FullName, "broken.json"));
        await WriteListFile(new DanceList
        {
            Categories =
            [
                TestData.CreateCategory("Region", dances:
                [
                    TestData.CreateDance("one", names: ["Hanter dro"]),
                    TestData.CreateDance("two", names: ["Hanter-dro"])
                ])
            ]
        }, broken.FullName);

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(broken));

        Assert.Equal(3, _sut.Current.AllDances.Count());
    }

    public void Dispose()
    {
        _sut.Dispose();
        if (_tempDir.Exists)
        {
            _tempDir.Delete(true);
        }
    }

    private string ListFilePath => Path.Combine(_tempDir.FullName, "dance_list.json");

    private async Task WriteListFile(DanceList list, string? path = null)
    {
        await using var stream = File.Create(path ?? ListFilePath);
        await JsonSerializer.SerializeAsync(stream, list, JsonOptions);
    }
}
