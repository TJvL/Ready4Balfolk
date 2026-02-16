using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Integration;

public sealed class TrackDurationCacheTests : IDisposable
{
    private readonly DirectoryInfo _tempDir;
    private readonly TrackDurationCache _sut;

    public TrackDurationCacheTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new TrackDurationCache(_tempDir);
    }

    [Fact]
    public async Task LoadAsync_NoFile_StartsFresh()
    {
        await _sut.LoadAsync();

        var result = _sut.TryGetDuration("/some/file.mp3", DateTime.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_StartsFresh()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir.FullName, "track_duration_cache.json"),
            "not valid json",
            TestContext.Current.CancellationToken);

        await _sut.LoadAsync();

        var result = _sut.TryGetDuration("/some/file.mp3", DateTime.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetDuration_AfterSet_ReturnsDuration()
    {
        await _sut.LoadAsync();

        var filePath = "/music/Dance - Artist - Title.mp3";
        var lastWrite = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromMinutes(3.5);

        _sut.SetDuration(filePath, lastWrite, duration);

        var result = _sut.TryGetDuration(filePath, lastWrite);
        Assert.Equal(duration, result);
    }

    [Fact]
    public async Task TryGetDuration_DifferentLastWriteTime_ReturnsNull()
    {
        await _sut.LoadAsync();

        var filePath = "/music/Dance - Artist - Title.mp3";
        var lastWrite = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var differentLastWrite = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        _sut.SetDuration(filePath, lastWrite, TimeSpan.FromMinutes(3));

        var result = _sut.TryGetDuration(filePath, differentLastWrite);
        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetDuration_UnknownFile_ReturnsNull()
    {
        await _sut.LoadAsync();

        var result = _sut.TryGetDuration("/unknown/file.mp3", DateTime.UtcNow);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndReloads()
    {
        await _sut.LoadAsync();

        var filePath = "/music/Dance - Artist - Title.mp3";
        var lastWrite = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var duration = TimeSpan.FromMinutes(4.2);

        _sut.SetDuration(filePath, lastWrite, duration);
        await _sut.SaveAsync(new HashSet<string> { filePath });

        var sut2 = new TrackDurationCache(_tempDir);
        await sut2.LoadAsync();

        var result = sut2.TryGetDuration(filePath, lastWrite);
        Assert.Equal(duration, result);
    }

    [Fact]
    public async Task SaveAsync_RemovesDeletedFiles()
    {
        await _sut.LoadAsync();

        var keepPath = "/music/keep.mp3";
        var deletedPath = "/music/deleted.mp3";
        var lastWrite = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        _sut.SetDuration(keepPath, lastWrite, TimeSpan.FromMinutes(3));
        _sut.SetDuration(deletedPath, lastWrite, TimeSpan.FromMinutes(4));

        await _sut.SaveAsync(new HashSet<string> { keepPath });

        var sut2 = new TrackDurationCache(_tempDir);
        await sut2.LoadAsync();

        Assert.NotNull(sut2.TryGetDuration(keepPath, lastWrite));
        Assert.Null(sut2.TryGetDuration(deletedPath, lastWrite));
    }

    public void Dispose()
    {
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
