using System.Text.Json;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Tree;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class DanceTreeStoreTests : IDisposable
{
    private static readonly JsonSerializerOptions SJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DirectoryInfo _tempDir;
    private readonly DanceTreeStore _sut;

    public DanceTreeStoreTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new DanceTreeStore(_tempDir);
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_LoadsBranches()
    {
        var branches = TestData.CreateSimpleTree();
        await WriteTreeFile(branches);

        await _sut.LoadAsync();

        Assert.Equal(2, _sut.Current.Count);
        Assert.Equal("Folk", _sut.Current[0].Name);
    }

    [Fact]
    public async Task LoadAsync_NoFile_KeepsEmpty()
    {
        await _sut.LoadAsync();
        Assert.Empty(_sut.Current);
    }

    [Fact]
    public async Task UpdateAsync_SavesAndPersists()
    {
        await _sut.UpdateAsync(roots =>
            DanceTreeTransforms.AddBranch(roots, []));

        Assert.Single(_sut.Current);

        // Verify file was written
        var filePath = Path.Combine(_tempDir.FullName, "dance_tree.json");
        Assert.True(File.Exists(filePath));

        // New store should load the same data
        using var store2 = new DanceTreeStore(_tempDir);
        await store2.LoadAsync();
        Assert.Single(store2.Current);
    }

    [Fact]
    public async Task ExportAsync_WritesFile()
    {
        await _sut.UpdateAsync(_ => TestData.CreateSimpleTree());
        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "tree.json"));

        await _sut.ExportAsync(exportFile);

        Assert.True(exportFile.Exists);
        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Folk", content);
    }

    [Fact]
    public async Task ImportAsync_ValidFile_Imports()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "import.json"));
        var branches = TestData.CreateSimpleTree();
        await using (var stream = File.Create(importFile.FullName))
        {
            await JsonSerializer.SerializeAsync(stream, branches, SJsonOptions, TestContext.Current.CancellationToken);
        }

        await _sut.ImportAsync(importFile);

        Assert.Equal(2, _sut.Current.Count);
        Assert.Equal("Folk", _sut.Current[0].Name);
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "bad.json"));
        await File.WriteAllTextAsync(importFile.FullName, "not valid json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    [Fact]
    public async Task ImportAsync_MissingFile_Throws()
    {
        var missingFile = new FileInfo(Path.Combine(_tempDir.FullName, "nope.json"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => _sut.ImportAsync(missingFile));
    }

    [Fact]
    public async Task ImportAsync_NullContent_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "null.json"));
        await File.WriteAllTextAsync(importFile.FullName, "null", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    [Fact]
    public async Task ImportAsync_NullBranchName_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "nullname.json"));
        await File.WriteAllTextAsync(importFile.FullName,
            """[{"name": null, "weight": 1, "children": [], "dances": []}]""",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    [Fact]
    public async Task Observe_EmitsOnUpdate()
    {
        var emissions = new List<IReadOnlyList<DanceBranch>>();
        using var sub = _sut.Observe().Subscribe(emissions.Add);

        await _sut.UpdateAsync(_ => TestData.CreateSimpleTree());

        Assert.True(emissions.Count >= 2); // initial empty + update
        Assert.Equal(2, emissions[^1].Count);
    }

    private async Task WriteTreeFile(IReadOnlyList<DanceBranch> branches)
    {
        var filePath = Path.Combine(_tempDir.FullName, "dance_tree.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, branches, SJsonOptions, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try
        {
            _tempDir.Delete(true);
        }
        catch
        {
            // cleanup best-effort
        }
    }
}
