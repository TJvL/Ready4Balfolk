using System.Diagnostics;
using System.IO.Abstractions;
using System.Reactive.Linq;
using NSubstitute;
using Ready4Balfolk.Domain;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Synonym;
using Ready4Balfolk.Domain.Services.Tracks;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

namespace Ready4Balfolk.Tests.Integration;

public sealed class TrackStoreTests : IDisposable
{
    private readonly IDirectoryInfo _tempDirA;
    private readonly IDirectoryInfo _tempDirB;
    private readonly ILoggerService _loggerService;
    private readonly TrackStore _sut;
    private readonly WatchableMockFileSystem _fileSystem;
    private readonly Dictionary<string, IFileSystemWatcher> _watchers = [];

    public TrackStoreTests()
    {
        SupportedAudioFormats.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3" });

        IFileSystemWatcher Watcher(string path)
        {
            var watcher = Substitute.For<IFileSystemWatcher>();

            _watchers[path] = watcher;
            return watcher;
        }

        _fileSystem = new WatchableMockFileSystem(Watcher);

        _tempDirA = _fileSystem.Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDirA.Create();
        _tempDirB = _fileSystem.Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
        _tempDirB.Create();

        _loggerService = Substitute.For<ILoggerService>();

        var discoveryService = Substitute.For<ITrackDiscoveryService>();
        discoveryService.LoadTrack(Arg.Any<IFileInfo>())
            .Returns(call => CreateTrackFor(call.Arg<IFileInfo>()!));
        discoveryService.LoadTrackWithDuration(Arg.Any<IFileInfo>(), Arg.Any<TimeSpan>())
            .Returns(call => CreateTrackFor(call.Arg<IFileInfo>()!));

        var synonymService = Substitute.For<ISynonymResolutionService>();
        synonymService.Resolve(Arg.Any<string>()).Returns(call => call.Arg<string>());
        synonymService.Changed.Returns(Observable.Never<System.Reactive.Unit>());

        var durationCache = Substitute.For<ITrackDurationCache>();
        durationCache.TryGetDuration(Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns((TimeSpan?)null);

        _sut = new TrackStore(_loggerService, discoveryService, synonymService, durationCache, _fileSystem);
    }

    [Fact]
    public async Task MusicDirectory_ChangedTwice_ReloadsAndKeepsWatching()
    {
        await _fileSystem.File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await _fileSystem.File.WriteAllTextAsync(
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
        await _fileSystem.File.WriteAllTextAsync(Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _watchers.ContainsKey(_tempDirB.FullName));
        var watcherB = _watchers[_tempDirB.FullName];
        watcherB.Created += Raise.Event<FileSystemEventHandler>(watcherB, new FileSystemEventArgs(WatcherChangeTypes.Created, _tempDirA.FullName, "c.mp3"));
        await WaitUntilAsync(() => _sut.Current.Any(t => t.FileInfo.Name == "c.mp3"));
    }

    [Fact]
    public async Task MusicDirectory_MissingDirectory_LogsWarningAndDoesNotStickLoading()
    {
        var directoryInfo = _fileSystem.DirectoryInfo.New(Path.Combine(Path.GetTempPath(), $"r4b_missing_{Guid.NewGuid():N}"));

        var isLoading = false;
        using var loadingSubscription = _sut.IsLoading.Subscribe(value => isLoading = value);

        _sut.MusicDirectory = directoryInfo;

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

    private static Track CreateTrackFor(IFileInfo fileInfo)
        => new("Mazurka", "Artist", fileInfo.Name, fileInfo,
            TimeSpan.FromSeconds(180), AudioFormat.Mp3);

    [Fact]
    public async Task MusicDirectory_SwitchedWhileLoading_SerialisesAndKeepsWatching()
    {
        _loggerService.ClearReceivedCalls();

        await _fileSystem.File.WriteAllTextAsync(
            Path.Combine(_tempDirA.FullName, "a.mp3"), "", TestContext.Current.CancellationToken);
        await _fileSystem.File.WriteAllTextAsync(
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
        await _fileSystem.File.WriteAllTextAsync(Path.Combine(_tempDirB.FullName, "c.mp3"), "", TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _watchers.ContainsKey(_tempDirB.FullName));
        var watcherB = _watchers[_tempDirB.FullName];
        watcherB.Created += Raise.Event<FileSystemEventHandler>(watcherB, new FileSystemEventArgs(WatcherChangeTypes.Created, _tempDirA.FullName, "c.mp3"));
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
