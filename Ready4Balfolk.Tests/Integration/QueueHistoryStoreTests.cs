using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Tests.Integration;

public sealed class QueueHistoryStoreTests : IDisposable
{
    private static readonly JsonSerializerOptions SJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DirectoryInfo _tempDir;
    private readonly QueueHistoryStore _sut;

    public QueueHistoryStoreTests()
    {
        _tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();
        _sut = new QueueHistoryStore(_tempDir);
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_LoadsHistory()
    {
        // Write a valid history file
        var history = new QueueHistory(DateTime.Now, [
            new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
                TimeSpan.FromMinutes(3), false, CompletionStatus.Finished)
        ]);
        await WriteHistoryFile(history);

        await _sut.LoadAsync();

        Assert.Single(_sut.Current.Entries);
        Assert.NotNull(_sut.Current.StartedAt);
    }

    [Fact]
    public async Task LoadAsync_NoFile_KeepsEmpty()
    {
        await _sut.LoadAsync();
        Assert.Empty(_sut.Current.Entries);
        Assert.Null(_sut.Current.StartedAt);
    }

    [Fact]
    public async Task AddAsync_AppendsEntry()
    {
        var entry = new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished);

        await _sut.AddAsync(entry);

        Assert.Single(_sut.Current.Entries);
        Assert.IsType<TrackHistoryEntry>(_sut.Current.Entries[0]);
    }

    [Fact]
    public async Task AddAsync_SetsStartedAtOnFirst()
    {
        var entry = new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished);

        Assert.Null(_sut.Current.StartedAt);
        await _sut.AddAsync(entry);
        Assert.NotNull(_sut.Current.StartedAt);
    }

    [Fact]
    public async Task AddAsync_PreservesStartedAtOnSubsequent()
    {
        var entry1 = new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished);
        await _sut.AddAsync(entry1);
        var firstStartedAt = _sut.Current.StartedAt;

        var entry2 = new DelayHistoryEntry(TimeSpan.FromSeconds(30), CompletionStatus.Finished);
        await _sut.AddAsync(entry2);

        Assert.Equal(firstStartedAt, _sut.Current.StartedAt);
    }

    [Fact]
    public async Task ClearAsync_ResetsHistory()
    {
        var entry = new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished);
        await _sut.AddAsync(entry);

        await _sut.ClearAsync();

        Assert.Empty(_sut.Current.Entries);
        Assert.Null(_sut.Current.StartedAt);
    }

    [Fact]
    public async Task ExportAsync_WritesFile()
    {
        var entry = new TrackHistoryEntry("/tmp/test.mp3", "Mazurka", "Artist", "Title",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished);
        await _sut.AddAsync(entry);

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "history.json"));
        await _sut.ExportAsync(exportFile);

        Assert.True(exportFile.Exists);
        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Mazurka", content);
    }

    [Fact]
    public async Task TotalDuration_SumsCorrectly()
    {
        await _sut.AddAsync(new TrackHistoryEntry("/tmp/a.mp3", "Mazurka", "A", "T",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished));
        await _sut.AddAsync(new DelayHistoryEntry(
            TimeSpan.FromSeconds(30), CompletionStatus.Finished));
        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        var total = _sut.Current.TotalDuration;
        Assert.Equal(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(30), total);
    }

    [Fact]
    public async Task Observe_EmitsOnAdd()
    {
        var emissions = new List<QueueHistory>();
        using var sub = _sut.Observe().Subscribe(emissions.Add);

        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        Assert.True(emissions.Count >= 2); // initial + update
    }

    private async Task WriteHistoryFile(QueueHistory history)
    {
        var filePath = Path.Combine(_tempDir.FullName, "queue_history.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, history, SJsonOptions);
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
