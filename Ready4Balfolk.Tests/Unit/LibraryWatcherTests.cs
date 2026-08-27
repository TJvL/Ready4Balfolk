using System.IO.Abstractions;
using NSubstitute;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The watcher on the music directory, which reports what changed and decides nothing about it.
/// </summary>
/// <remarks>
/// Lifetime and event plumbing that used to live inside TrackStore, reachable from a test only by
/// driving a whole scan first.
/// </remarks>
public sealed class LibraryWatcherTests : IDisposable
{
    private readonly List<(string Path, IFileSystemWatcher Watcher)> _watchers = [];
    private readonly WatchableMockFileSystem _fileSystem;
    private readonly LibraryWatcher _sut;
    private readonly List<LibraryFileChange> _seen = [];
    private readonly IDisposable _subscription;

    public LibraryWatcherTests()
    {
        _fileSystem = new WatchableMockFileSystem(CreateWatcher);
        _sut = new LibraryWatcher(_fileSystem);
        _subscription = _sut.Changes.Subscribe(_seen.Add);
    }

    private IFileSystemWatcher CreateWatcher(string path)
    {
        var watcher = Substitute.For<IFileSystemWatcher>();
        watcher.Path.Returns(path);
        _watchers.Add((path, watcher));
        return watcher;
    }

    private IDirectoryInfo Directory(string path)
    {
        var directory = _fileSystem.DirectoryInfo.New(path);
        directory.Create();
        return directory;
    }

    private IFileSystemWatcher Latest => _watchers[^1].Watcher;

    [Fact]
    public void Watch_EnablesAWatcherOnTheDirectory()
    {
        var directory = Directory("/music");

        _sut.Watch(directory);

        Assert.Single(_watchers);
        Assert.Equal(directory.FullName, _watchers[0].Path);
        // Configured but never enabled reports nothing, which is the failure this guards against.
        Assert.True(Latest.EnableRaisingEvents);
    }

    [Fact]
    public void Created_IsReportedAsAppeared()
    {
        _sut.Watch(Directory("/music"));

        Latest.Created += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Created, "/music", "a.mp3"));

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Appeared, change.Kind);
        Assert.EndsWith("a.mp3", change.Path, StringComparison.Ordinal);
        Assert.Null(change.PreviousPath);
    }

    [Fact]
    public void Deleted_IsReportedAsVanished()
    {
        _sut.Watch(Directory("/music"));

        Latest.Deleted += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Deleted, "/music", "a.mp3"));

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Vanished, change.Kind);
    }

    [Fact]
    public void Renamed_CarriesBothEnds()
    {
        // The audio is unchanged across a rename, so the new path inherits everything approved
        // about the old one. A report that dropped the old path could not express that.
        _sut.Watch(Directory("/music"));

        Latest.Renamed += Raise.Event<RenamedEventHandler>(
            Latest, new RenamedEventArgs(WatcherChangeTypes.Renamed, "/music", "new.mp3", "old.mp3"));

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Renamed, change.Kind);
        Assert.EndsWith("new.mp3", change.Path, StringComparison.Ordinal);
        Assert.EndsWith("old.mp3", change.PreviousPath!, StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_Twice_LeavesOnlyTheNewerWatcherReporting()
    {
        // Switching the music directory used to leave the old subscriptions attached, so a file
        // touched under the directory nobody is using any more still reached the store.
        _sut.Watch(Directory("/music/a"));
        var first = Latest;

        _sut.Watch(Directory("/music/b"));

        first.Created += Raise.Event<FileSystemEventHandler>(
            first, new FileSystemEventArgs(WatcherChangeTypes.Created, "/music/a", "stale.mp3"));

        Assert.Empty(_seen);

        Latest.Created += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Created, "/music/b", "fresh.mp3"));

        Assert.Single(_seen);
    }

    [Fact]
    public void Stop_LeavesItUsableAgain()
    {
        _sut.Watch(Directory("/music"));
        _sut.Stop();

        _sut.Watch(Directory("/music"));
        Latest.Created += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Created, "/music", "a.mp3"));

        Assert.Single(_seen);
    }

    [Fact]
    public void Watch_AfterDispose_DoesNothing()
    {
        // Enabling a disposed watcher is what threw ObjectDisposedException, and a watcher nobody
        // will dispose is worse than no watcher at all.
        _sut.Dispose();

        _sut.Watch(Directory("/music"));

        Assert.Empty(_watchers);
    }

    [Fact]
    public void Dispose_Twice_IsSafe()
    {
        _sut.Watch(Directory("/music"));

        _sut.Dispose();
        _sut.Dispose();
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _sut.Dispose();
    }
}

