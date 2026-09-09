using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NSubstitute;
using Ready4Balfolk.Domain.Models.History;
using Ready4Balfolk.Domain.Resources;
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
    public async Task ExportAsync_WritesAccentedNamesAsThemselvesRatherThanAsEscapes()
    {
        // An export is opened and read by whoever asked for one, and half the repertoire is
        // spelled with something outside ASCII. The default encoder escapes those so that JSON is
        // safe to drop into an HTML page; nothing embeds this file, so the escapes cost a reader
        // and buy nothing.
        await _sut.AddAsync(new TrackHistoryEntry(
            TrackPath, "Bourrée", "Naragonia", "Ó Riada",
            TimeSpan.FromMinutes(3), false, CompletionStatus.Finished, DateTime.Now));

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "history.json"));
        await _sut.ExportAsync(_sut.Current.Id, exportFile.FullName);

        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Bourrée", content, StringComparison.Ordinal);
        Assert.Contains("Ó Riada", content, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00", content, StringComparison.Ordinal);

        // And it is still JSON, which is the half of the trade that must not have been given away.
        using var parsed = JsonDocument.Parse(content);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    [Fact]
    public async Task ExportReportAsync_WritesTheDocumentWithNoByteOrderMarkInFrontOfIt()
    {
        await _sut.AddAsync(Track());

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "night.html"));
        await _sut.ExportReportAsync(_sut.Current.Id, exportFile.FullName);

        // The document says its own encoding in its head. A mark in front of the doctype is
        // content as far as the browser is concerned, and it draws it above the heading.
        var bytes = await File.ReadAllBytesAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Equal("<!doctype"u8.ToArray(), bytes[..9]);
    }

    [Fact]
    public async Task ExportSpreadsheetAsync_WritesTheRowsWithAByteOrderMarkSoExcelReadsThemAsUtf8()
    {
        await _sut.AddAsync(Track() with { Artist = "Naragonia", Title = "Bourrée" });

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "night.csv"));
        await _sut.ExportSpreadsheetAsync(_sut.Current.Id, exportFile.FullName);

        // The opposite call to the report's, and for the opposite reason: CSV carries no way to say
        // what it is encoded in, so Excel opens one without a mark in the machine's own code page
        // and every accented name in the evening arrives as mojibake.
        var bytes = await File.ReadAllBytesAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Naragonia,Bourrée", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_LeavesOutWhereTheFilesAre()
    {
        await _sut.AddAsync(Track());
        var nightId = _sut.Current.Id;

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "history.json"));
        await _sut.ExportAsync(nightId, exportFile.FullName);

        // An export goes to an organiser. Dance, artist, title and times are the evening; a path
        // is a description of the DJ's disk.
        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);
        Assert.Contains("Mazurka", content, StringComparison.Ordinal);
        Assert.Contains("\"track\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(TrackHistoryEntry.FilePath), content, StringComparison.Ordinal);
        Assert.DoesNotContain(TrackPath, content, StringComparison.Ordinal);

        // And it is still in the database, because that is what the duplicate rule and the random
        // picker recognise a played track by.
        var reopened = await ReopenAsync();
        Assert.Equal(
            TrackPath,
            Assert.IsType<TrackHistoryEntry>(reopened.Current.Entries[0]).FilePath);
    }

    [Fact]
    public async Task EndNightAsync_WithATime_FilesTheNightAtThatTimeRatherThanNow()
    {
        // A night nobody ended is noticed at the next start, which can be days later. Stamping it
        // with the moment somebody answered would put an evening in the books that ran until then.
        var stopped = DateTime.Now.AddHours(-9);
        await _sut.AddAsync(Track());
        var filed = _sut.Current.Id;

        await _sut.EndNightAsync(stopped);

        var night = await _sut.ReadNightAsync(filed);

        Assert.NotNull(night);
        Assert.Equal(stopped, night.EndedAt);
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
    public async Task ExportReportAsync_WritesTheTracksThatWerePlayedAsADocument()
    {
        await _sut.AddAsync(Track() with { Artist = "Naragonia", Title = "Salamandre" });

        var exportFile = new FileInfo(Path.Combine(_tempDir.FullName, "export", "history.html"));
        await _sut.ExportReportAsync(_sut.Current.Id, exportFile.FullName);

        Assert.True(exportFile.Exists);
        var content = await File.ReadAllTextAsync(exportFile.FullName, TestContext.Current.CancellationToken);

        // What a rights organisation is being shown: who played what, under a heading, and no
        // description of the DJ's disk.
        Assert.StartsWith("<!doctype html>", content, StringComparison.Ordinal);
        Assert.Contains(DomainStrings.NightReport_Heading, content, StringComparison.Ordinal);
        Assert.Contains("Naragonia", content, StringComparison.Ordinal);
        Assert.Contains("Salamandre", content, StringComparison.Ordinal);
        Assert.DoesNotContain(TrackPath, content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Observe_EmitsOnAdd()
    {
        var emissions = new List<QueueHistory>();
        using var subscription = _sut.Observe().Subscribe(emissions.Add);

        await _sut.AddAsync(new StopHistoryEntry(CompletionStatus.Finished));

        Assert.True(emissions.Count >= 2); // initial + update
    }

    private const string TrackPath = "/tmp/test.mp3";

    private static TrackHistoryEntry Track() => new(
        TrackPath, "Mazurka", "Artist", "Title",
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
