using System.Text.Json;
using Ready4Balfolk.Domain.Models.Synonyms;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Synonym;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class DanceSynonymStoreTests : IDisposable
{
    private static readonly JsonSerializerOptions SJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DirectoryInfo _tempDir;
    private readonly DanceSynonymStore _sut;

    public DanceSynonymStoreTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new DanceSynonymStore(_tempDir);
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_LoadsSynonyms()
    {
        var data = TestData.CreateSimpleSynonyms();
        await WriteSynonymFile(data);

        await _sut.LoadAsync();

        Assert.Equal(2, _sut.Current.Count);
        Assert.Equal("Mazurka", _sut.Current[0].Name);
    }

    [Fact]
    public async Task LoadAsync_NoFile_KeepsEmpty()
    {
        await _sut.LoadAsync();
        Assert.Empty(_sut.Current);
    }

    [Fact]
    public async Task UpdateAsync_Persists()
    {
        await _sut.UpdateAsync(DanceSynonymTransforms.AddMainName);

        Assert.Single(_sut.Current);

        using var store2 = new DanceSynonymStore(_tempDir);
        await store2.LoadAsync();
        Assert.Single(store2.Current);
    }

    [Fact]
    public async Task ImportAsync_ValidFile_Imports()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "import.json"));
        var data = TestData.CreateSimpleSynonyms();
        await using (var stream = File.Create(importFile.FullName))
        {
            await JsonSerializer.SerializeAsync(stream, data, SJsonOptions, TestContext.Current.CancellationToken);
        }

        await _sut.ImportAsync(importFile);

        Assert.Equal(2, _sut.Current.Count);
    }

    [Fact]
    public async Task ImportAsync_DuplicateNames_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "dup.json"));
        var data = new List<DanceMainName>
        {
            TestData.CreateMainName("Mazurka"), TestData.CreateMainName("Mazurka")
        };
        await using (var stream = File.Create(importFile.FullName))
        {
            await JsonSerializer.SerializeAsync(stream, data, SJsonOptions, TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "bad.json"));
        await File.WriteAllTextAsync(importFile.FullName, "not json", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    [Fact]
    public async Task ImportAsync_EmptyNames_Throws()
    {
        var importFile = new FileInfo(Path.Combine(_tempDir.FullName, "empty.json"));
        var data = new List<DanceMainName>
        {
            new("", [])
        };
        await using (var stream = File.Create(importFile.FullName))
        {
            await JsonSerializer.SerializeAsync(stream, data, SJsonOptions, TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => _sut.ImportAsync(importFile));
    }

    private async Task WriteSynonymFile(IReadOnlyList<DanceMainName> data)
    {
        var filePath = Path.Combine(_tempDir.FullName, "dance_synonyms.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, SJsonOptions, TestContext.Current.CancellationToken);
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
