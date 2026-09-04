using System.IO.Abstractions;
using Microsoft.Data.Sqlite;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.History;

namespace Ready4Balfolk.Tests.Integration;

public sealed class QueueHistoryStoreTests : IDisposable
{
    private readonly IDirectoryInfo _tempDir;
    private readonly FileSystem _fileSystem;
    private readonly IApplicationSettingsDirectory _directory;
    private readonly QueueHistoryStore _sut;
    private readonly List<QueueHistoryStore> _reopened = [];

    public QueueHistoryStoreTests()
    {
        _fileSystem = new FileSystem();
        _tempDir = _fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDir.Create();

        _directory = Substitute.For<IApplicationSettingsDirectory>();
        _directory.DirectoryInfoRoot.Returns(_ => _tempDir);
        _sut = new QueueHistoryStore(_directory, _fileSystem, new NoOpLoggerService(), TimeProvider.System);
    }

    [Fact]
    public async Task LoadAsync_NoDatabase_KeepsEmpty()
    {
        await _sut.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_sut.Current.Entries);
        Assert.Null(_sut.Current.StartedAt);
        Assert.True(_sut.Current.IsOpen);
    }

    [Fact]
    public async Task AddAsync_AppendsEntry()
    {
        await _sut.AddAsync(Track());

        Assert.Single(_sut.Current.Entries);
        Assert.IsType<TrackHistoryEntry>(_sut.Current.Entries[0]);
    }

    [Fact]
    public async Task AddAsync_SetsStartedAtOnFirst()
    {
        Assert.Null(_sut.Current.StartedAt);

        await _sut.AddAsync(Track());

        Assert.NotNull(_sut.Current.StartedAt);
    }

    [Fact]
    public async Task AddAsync_PreservesStartedAtOnSubsequent()
    {
        await _sut.AddAsync(Track());
        var firstStartedAt = _sut.Current.StartedAt;

        await _sut.AddAsync(new DelayHistoryEntry(TimeSpan.FromSeconds(30), CompletionStatus.Finished));

        Assert.Equal(firstStartedAt, _sut.Current.StartedAt);
    }

    /// <summary>Closing the application mid-evening does not begin a second night.</summary>
    [Fact]
    public async Task AddAsync_SurvivesAReopen()
    {
        await _sut.AddAsync(Track());
        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        var reopened = await ReopenAsync();

        Assert.Equal(2, reopened.Current.Entries.Count);
        Assert.IsType<TrackHistoryEntry>(reopened.Current.Entries[0]);
        Assert.IsType<StopHistoryEntry>(reopened.Current.Entries[1]);
        Assert.NotNull(reopened.Current.StartedAt);
    }

    [Fact]
    public async Task EndNightAsync_LeavesTheNextNightEmpty()
    {
        await _sut.AddAsync(Track());

        await _sut.EndNightAsync();

        Assert.Empty(_sut.Current.Entries);
        Assert.Null(_sut.Current.StartedAt);
        Assert.True(_sut.Current.IsOpen);
    }

    /// <summary>Ending a night files it. Nothing is thrown away, which is the whole of the design.</summary>
    [Fact]
    public async Task EndNightAsync_KeepsTheNightThatFinished()
    {
        await _sut.AddAsync(Track());

        await _sut.EndNightAsync();

        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM nights WHERE ended_at IS NOT NULL;"));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM entries;"));

        // And the filed night is not handed back as the current one.
        var reopened = await ReopenAsync();
        Assert.Empty(reopened.Current.Entries);
    }

    [Fact]
    public async Task EndNightAsync_WithNothingInIt_WritesNothing()
    {
        await _sut.LoadAsync(TestContext.Current.CancellationToken);

        await _sut.EndNightAsync();

        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM nights;"));
    }

    [Fact]
    public async Task DeleteNightAsync_ThrowsTheNightAway()
    {
        await _sut.AddAsync(Track());

        await _sut.DeleteNightAsync(_sut.Current.Id);

        Assert.Empty(_sut.Current.Entries);
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM nights;"));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM entries;"));
    }

    [Fact]
    public async Task ExportAsync_WritesFile()
    {
        await _sut.AddAsync(Track());

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "history.json"));
        await _sut.ExportAsync(_sut.Current.Id, exportFile.FullName);

        Assert.True(exportFile.Exists);
        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Mazurka", content);
    }

    [Fact]
    public async Task ListNightsAsync_HasTheNightsNewestFirst()
    {
        await _sut.AddAsync(Track());
        await _sut.EndNightAsync();
        await _sut.AddAsync(Track());

        var nights = await _sut.ListNightsAsync();

        Assert.Equal(2, nights.Count);
        Assert.True(nights[0].IsOpen, "The night that is running was not the first one offered.");
        Assert.False(nights[1].IsOpen, "A night that was filed still reads as running.");
        Assert.Equal(1, nights[1].Entries);
    }

    [Fact]
    public async Task ReadNightAsync_ReadsBackAnEveningThatWasFiled()
    {
        await _sut.AddAsync(Track());
        var filed = _sut.Current.Id;
        await _sut.EndNightAsync();

        var night = await _sut.ReadNightAsync(filed);

        Assert.NotNull(night);
        Assert.Single(night.Entries);
        Assert.NotNull(night.EndedAt);
        Assert.Contains(night.Entries, entry => entry is TrackHistoryEntry { Dance: "Mazurka" });
    }

    [Fact]
    public async Task DeleteNightAsync_ThrowsAwayAFiledNightAndLeavesTonightAlone()
    {
        await _sut.AddAsync(Track());
        var filed = _sut.Current.Id;
        await _sut.EndNightAsync();
        await _sut.AddAsync(Track());

        await _sut.DeleteNightAsync(filed);

        Assert.Single(_sut.Current.Entries);
        Assert.Single(await _sut.ListNightsAsync());
    }

    [Fact]
    public async Task ExportAsync_WritesAFiledNightRatherThanTheOneRunning()
    {
        await _sut.AddAsync(Track());
        var filed = _sut.Current.Id;
        await _sut.EndNightAsync();
        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "filed.json"));
        await _sut.ExportAsync(filed, exportFile.FullName);

        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Mazurka", content);
        Assert.DoesNotContain("\"stop\"", content);
    }

    [Fact]
    public async Task Observe_EmitsOnAdd()
    {
        var emissions = new List<QueueHistory>();
        using var subscription = _sut.Observe().Subscribe(emissions.Add);

        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        Assert.True(emissions.Count >= 2); // initial + update
    }

    private static TrackHistoryEntry Track() => new(
        "/tmp/test.mp3", "Mazurka", "Artist", "Title",
        TimeSpan.FromMinutes(3), false, CompletionStatus.Finished, DateTime.Now);

    /// <summary>A second store over the same directory, which is what a restart amounts to.</summary>
    private async Task<QueueHistoryStore> ReopenAsync()
    {
        var reopened = new QueueHistoryStore(_directory, _fileSystem, new NoOpLoggerService(), TimeProvider.System);
        _reopened.Add(reopened);
        await reopened.LoadAsync(TestContext.Current.CancellationToken);
        return reopened;
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_tempDir.FullName, "history.sqlite"),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public void Dispose()
    {
        _sut.Dispose();
        foreach (var store in _reopened)
        {
            store.Dispose();
        }

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
