using Ready4Balfolk.Domain.Services.Dances;

namespace Ready4Balfolk.Tests.Integration;

public sealed class BigBalfolkListImporterTests : IDisposable
{
    private const string SampleJson = """
        {
          "formatVersion": 2,
          "dances": [
            { "slug": "scottish", "names": ["Scottish", "Schottische"], "region": "Common", "family": null, "suite": null, "tags": ["couple"] },
            { "slug": "mazurka", "names": ["Mazurka", "Mazurca"], "region": "Common", "family": null, "suite": null, "tags": ["couple"] },
            { "slug": "waltz-in-3", "names": ["Waltz in 3"], "region": "Common", "family": "Waltzes", "suite": null, "tags": [] },
            { "slug": "waltz-in-5", "names": ["Waltz in 5"], "region": "Common", "family": "Waltzes", "suite": null, "tags": [] },
            { "slug": "an-dro", "names": ["An dro", "Andro"], "region": "Bretagne (France)", "family": null, "suite": null, "tags": [] },
            { "slug": "ton-doubl-plinn", "names": ["Ton doubl (plinn)"], "region": "Bretagne (France)", "family": null, "suite": "Suite plinn", "tags": [] }
          ]
        }
        """;

    private readonly DirectoryInfo _tempDir;

    public BigBalfolkListImporterTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
    }

    [Fact]
    public async Task ReadAsync_RegionBecomesATopLevelCategory()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        Assert.Equal(["Common", "Bretagne (France)"], list.Categories.Select(c => c.Name));
    }

    [Fact]
    public async Task ReadAsync_FamilyBecomesASubCategory()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        var common = list.Categories[0];
        var waltzes = Assert.Single(common.Categories);
        Assert.Equal("Waltzes", waltzes.Name);
        Assert.Equal(["waltz-in-3", "waltz-in-5"], waltzes.Dances.Select(d => d.Slug));
    }

    [Fact]
    public async Task ReadAsync_SuiteBecomesASubCategoryToo()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        var bretagne = list.Categories[1];
        Assert.Equal("Suite plinn", Assert.Single(bretagne.Categories).Name);
    }

    [Fact]
    public async Task ReadAsync_DanceWithNeitherFamilyNorSuite_SitsInTheRegion()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        Assert.Equal(["scottish", "mazurka"], list.Categories[0].Dances.Select(d => d.Slug));
    }

    [Fact]
    public async Task ReadAsync_EverythingStartsAtWeightOne()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        Assert.All(list.AllDances, dance => Assert.Equal(1, dance.Weight));
        Assert.All(list.Categories, category => Assert.Equal(1, category.Weight));
    }

    [Fact]
    public async Task ReadAsync_KeepsEveryNameInFileOrder()
    {
        var list = await BigBalfolkListImporter.ReadAsync(Write(SampleJson), TestContext.Current.CancellationToken);

        var scottish = list.AllDances.First(d => d.Slug == "scottish");
        Assert.Equal(["Scottish", "Schottische"], scottish.Names);
        Assert.Equal("Scottish", scottish.DisplayName);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_Throws()
    {
        var missing = new FileInfo(Path.Combine(_tempDir.FullName, "nope.json"));

        await Assert.ThrowsAsync<FileNotFoundException>(() => BigBalfolkListImporter.ReadAsync(missing, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_NotJson_Throws()
    {
        var file = Write("not json at all");

        await Assert.ThrowsAsync<InvalidDataException>(() => BigBalfolkListImporter.ReadAsync(file, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_JsonThatIsNotADanceList_Throws()
    {
        var file = Write("""{ "somethingElse": true }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => BigBalfolkListImporter.ReadAsync(file, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_NameClaimedByTwoDances_IsRefused()
    {
        var file = Write("""
            {
              "formatVersion": 2,
              "dances": [
                { "slug": "one", "names": ["Ton doubl"], "region": "Bretagne (France)" },
                { "slug": "two", "names": ["Ton-doubl"], "region": "Bretagne (France)" }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => BigBalfolkListImporter.ReadAsync(file, TestContext.Current.CancellationToken));

        // The message has to name the offender: hunting for the collision by hand is the work the
        // application already did.
        Assert.Contains("Ton-doubl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_EntryWithoutASlugOrName_IsSkipped()
    {
        var file = Write("""
            {
              "formatVersion": 2,
              "dances": [
                { "slug": "", "names": ["Nameless"], "region": "Common" },
                { "slug": "empty", "names": [], "region": "Common" },
                { "slug": "andro", "names": ["An dro"], "region": "Common" }
              ]
            }
            """);

        var list = await BigBalfolkListImporter.ReadAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal(["andro"], list.AllDances.Select(d => d.Slug));
    }

    public void Dispose()
    {
        if (_tempDir.Exists)
        {
            _tempDir.Delete(true);
        }
    }

    private FileInfo Write(string contents)
    {
        var path = Path.Combine(_tempDir.FullName, "dances.json");
        File.WriteAllText(path, contents);
        return new FileInfo(path);
    }
}
