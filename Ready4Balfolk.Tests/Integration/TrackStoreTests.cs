using System.Diagnostics;
using System.Reactive.Linq;
using NSubstitute;
using Ready4Balfolk.Domain;
using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Dances;
using Ready4Balfolk.Domain.Stores.Library;
using Ready4Balfolk.Domain.Stores.Tracks;

namespace Ready4Balfolk.Tests.Integration;

public sealed class TrackStoreTests : IDisposable
{
    private readonly DirectoryInfo _tempDirA;
    private readonly DirectoryInfo _tempDirB;
    private readonly ILoggerService _loggerService;
    private readonly ITrackDiscoveryService _discoveryService;
    private readonly ILibraryIndex _libraryIndex;
    private Dictionary<string, LibraryEntry> _indexSnapshot = [];
    private readonly TrackStore _sut;

    public TrackStoreTests()
    {
        SupportedAudioFormats.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3" });

        _tempDirA = CreateTempDirectory();
        _tempDirB = CreateTempDirectory();

        _loggerService = Substitute.For<ILoggerService>();

        _discoveryService = Substitute.For<ITrackDiscoveryService>();
        var discoveryService = _discoveryService;
        discoveryService.LoadTrack(Arg.Any<FileInfo>())
            .Returns(call => CreateTrackFor(call.Arg<FileInfo>()!));
        discoveryService.LoadTrackWithDuration(Arg.Any<FileInfo>(), Arg.Any<TimeSpan>())
            .Returns(call => CreateTrackFor(call.Arg<FileInfo>()!));
        discoveryService.Scan(Arg.Any<FileInfo>())
            .Returns(call => new ScannedAudioFile("Mazurka", "Artist", call.Arg<FileInfo>()!.Name,
                TimeSpan.FromSeconds(180), AudioFormat.Mp3, [1, 2, 3]));

        // An empty list: every track stays unresolved, keeping the name the file gave it, which is
        // what these tests assert on.
        var danceListStore = Substitute.For<IDanceListStore>();
        danceListStore.Current.Returns(DanceList.Empty);
        danceListStore.Index.Returns(DanceListIndex.Empty);
        danceListStore.Observe().Returns(Observable.Never<DanceList>());

        // An index that knows nothing, so every file is read: these tests are about the store's
        // discovery and watching, not about what the index remembers.
        _libraryIndex = Substitute.For<ILibraryIndex>();
        _libraryIndex.SnapshotByPathAsync(Arg.Any<CancellationToken>())
            .Returns(_ => _indexSnapshot);

        _sut = new TrackStore(_loggerService, discoveryService, danceListStore, _libraryIndex);
    }

    [Fact]
    public async Task MusicDirectory_ChangedTwice_ReloadsAndKeepsWatching()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "b.mp3"), "", TestContext.Current.CancellationToken);

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "a.mp3"));

        // Switching a second time used to throw a NullReferenceException from the
        // watcher remove-handlers and leave the store without a FileSystemWatcher.
        _sut.MusicDirectory = _tempDirB;
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));
        await WaitUntilAsync(() => !isLoading);

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The watcher must be re-attached to the new directory: a file created
        // after the switch has to show up in the store.
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    [Fact]
    public async Task MusicDirectory_MissingDirectory_LogsWarningAndDoesNotStickLoading()
    {
        var missing = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), $"r4b_missing_{Guid.NewGuid():N}"));

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = missing;

        await WaitUntilAsync(() => _loggerService.ReceivedCalls()
            .Any(c => c.GetMethodInfo().Name == nameof(ILoggerService.WarningAsync)));
        Assert.False(isLoading);
        Assert.Empty(_sut.Current);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try
        {
            _tempDirA.Delete(true);
            _tempDirB.Delete(true);
        }
        catch
        {
            // cleanup best-effort
        }
    }

    private static DirectoryInfo CreateTempDirectory()
    {
        var directory = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        directory.Create();
        return directory;
    }

    [Fact]
    public async Task UnchangedFile_IsNotOpened()
    {
        var path = Path.Combine(_tempDirA.FullName, "known.mp3");
        await File.WriteAllTextAsync(path, "audio", TestContext.Current.CancellationToken);
        var file = new FileInfo(path);
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            [path] = IndexedAs(file, "Mazurka")
        };

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        // The whole point of the index: a startup that finds nothing changed touches no audio.
        _discoveryService.DidNotReceiveWithAnyArgs().Scan(default!);
        Assert.Equal("Mazurka", _sut.Current[0].Dance);
        Assert.Equal(TimeSpan.FromSeconds(42), _sut.Current[0].Length);
    }

    [Fact]
    public async Task ChangedFile_IsReadAgain()
    {
        var path = Path.Combine(_tempDirA.FullName, "changed.mp3");
        await File.WriteAllTextAsync(path, "audio", TestContext.Current.CancellationToken);
        var file = new FileInfo(path);
        _indexSnapshot = new Dictionary<string, LibraryEntry>(StringComparer.Ordinal)
        {
            // A size the file no longer has, which is what "this changed" looks like without
            // opening it.
            [path] = IndexedAs(file, "Mazurka") with { FileSize = file.Length + 1 }
        };

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        _discoveryService.ReceivedWithAnyArgs().Scan(default!);
    }

    [Fact]
    public async Task FileTheIndexHasNeverSeen_IsReadAndWrittenBack()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "fresh.mp3"), "audio", TestContext.Current.CancellationToken);

        _sut.MusicDirectory = _tempDirA;
        await WaitUntilAsync(() => _sut.Current.Count == 1);

        await _libraryIndex.ReceivedWithAnyArgs().WriteAsync(default!, TestContext.Current.CancellationToken);
    }

    private static LibraryEntry IndexedAs(FileInfo file, string dance) => new()
    {
        ContentHash = [7],
        Path = file.FullName,
        FileSize = file.Length,
        LastWriteUtc = file.LastWriteTimeUtc,
        Duration = TimeSpan.FromSeconds(42),
        Format = AudioFormat.Mp3,
        DanceSlug = null,
        OriginalDance = dance,
        Artist = "Artist",
        Title = file.Name
    };

    private static Track CreateTrackFor(FileInfo fileInfo)
        => new("Mazurka", "Artist", fileInfo.Name, fileInfo,
            TimeSpan.FromSeconds(180), AudioFormat.Mp3);

    [Fact]
    public async Task MusicDirectory_SwitchedWhileLoading_SerialisesAndKeepsWatching()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "b.mp3"), "", TestContext.Current.CancellationToken);

        // Switch without waiting, so the second load starts while the first is still running.
        // Unserialised, the two race over _watcher and _tracks: one disposes the watcher the
        // other just published, and enabling it then throws ObjectDisposedException.
        _sut.MusicDirectory = _tempDirA;
        _sut.MusicDirectory = _tempDirB;

        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "b.mp3"));

        await _loggerService.DidNotReceive().ErrorAsync(Arg.Any<string>(), Arg.Any<Exception>());
        Assert.DoesNotContain(_sut.Current, t => t.FileInfo.Name == "a.mp3");

        // The surviving load must still own a live watcher on its own directory.
        await File.WriteAllTextAsync(
            Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                "Timed out waiting for condition");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
