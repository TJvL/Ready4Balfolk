using System.IO.Abstractions;
using System.Reactive.Concurrency;
using NSubstitute;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores.Tracks;
using Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The watcher on the music directory, which reports what changed and decides nothing about it.
/// </summary>
/// <remarks>
/// Lifetime and event plumbing that used to live inside TrackStore, reachable from a test only by
/// driving a whole scan first. Time is virtual here: the settling and the retries are scheduled,
/// and a test that waited on a real clock would be slow where it passed and flaky where it did not.
/// </remarks>
public sealed class LibraryWatcherTests : IDisposable
{
    private static readonly TimeSpan SettleFor = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(10);

    private readonly List<(string Path, IFileSystemWatcher Watcher)> _watchers = [];
    private readonly WatchableMockFileSystem _fileSystem;
    private readonly ILoggerService _loggerService = Substitute.For<ILoggerService>();
    private readonly HistoricalScheduler _scheduler = new();
    private readonly LibraryWatcher _sut;
    private readonly List<LibraryFileChange> _seen = [];
    private readonly IDisposable _subscription;

    public LibraryWatcherTests()
    {
        _fileSystem = new WatchableMockFileSystem(CreateWatcher);
        _sut = new LibraryWatcher(_fileSystem, _loggerService, SettleFor, RetryEvery, _scheduler);
        _subscription = _sut.Changes.Subscribe(_seen.Add);
    }

    /// <summary>How many more times asking for a watcher throws, as a mount that came and went.</summary>
    private int _watchersThatCannotBeOpened;

    private IFileSystemWatcher CreateWatcher(string path)
    {
        if (_watchersThatCannotBeOpened > 0)
        {
            _watchersThatCannotBeOpened--;

            // What the real factory throws for a directory that is not there.
            throw new ArgumentException($"The directory name '{path}' is invalid.", nameof(path));
        }

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

    /// <summary>Writes a file the way the copy that raised the event would have.</summary>
    private string WriteFile(IDirectoryInfo directory, string name, string content = "audio")
    {
        // Combined rather than spelled, because the assertions compare against what the file
        // system produced and a spelled path is a different string on Windows.
        var path = _fileSystem.Path.Combine(directory.FullName, name);
        _fileSystem.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Whether the watcher asked to be told about this kind of change at all.</summary>
    /// <remarks>
    /// The event helpers below go through this, so a test drives a watcher that reports what the
    /// platform would report and nothing else. ReadDirectoryChangesW is handed the filter: a name
    /// change arrives only under FileName for a file and only under DirectoryName for a folder,
    /// and a write only under LastWrite or Size. The inotify backend makes none of those
    /// distinctions, so a filter that cannot see a folder being renamed on the DJ's Windows
    /// machine looks perfectly healthy on the Linux one it was written on, and a test that raised
    /// the event regardless would agree with it.
    /// </remarks>
    private bool AsksAbout(NotifyFilters kinds) => (Latest.NotifyFilter & kinds) != 0;

    private void RaiseCreated(IDirectoryInfo directory, string name)
    {
        if (!AsksAbout(NotifyFilters.FileName))
        {
            return;
        }

        Latest.Created += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Created, directory.FullName, name));
    }

    /// <summary>A write to something already there, which is a retag or a copy still running.</summary>
    private void RaiseChanged(IDirectoryInfo directory, string name)
    {
        if (!AsksAbout(NotifyFilters.LastWrite | NotifyFilters.Size))
        {
            return;
        }

        Latest.Changed += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Changed, directory.FullName, name));
    }

    private void RaiseDeleted(IDirectoryInfo directory, string name, NotifyFilters kind)
    {
        if (!AsksAbout(kind))
        {
            return;
        }

        Latest.Deleted += Raise.Event<FileSystemEventHandler>(
            Latest, new FileSystemEventArgs(WatcherChangeTypes.Deleted, directory.FullName, name));
    }

    private void RaiseRenamed(IDirectoryInfo directory, string name, string oldName, NotifyFilters kind)
    {
        if (!AsksAbout(kind))
        {
            return;
        }

        Latest.Renamed += Raise.Event<RenamedEventHandler>(
            Latest, new RenamedEventArgs(WatcherChangeTypes.Renamed, directory.FullName, name, oldName));
    }

    /// <summary>Lets a path go quiet for longer than the settling window.</summary>
    private void LetItSettle() => _scheduler.AdvanceBy(SettleFor + TimeSpan.FromSeconds(1));

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
    public void Watch_AsksForABufferThatHoldsABurst()
    {
        // The buffer the operating system fills while nobody is reading it. An album copied in
        // overruns the 8 KB default, and what does not fit is dropped with no error but Error.
        _sut.Watch(Directory("/music"));

        Assert.True(Latest.InternalBufferSize > 8 * 1024);
    }

    [Fact]
    public void Created_IsReportedAsAppeared_OnceTheFileHoldsStill()
    {
        var directory = Directory("/music");
        _sut.Watch(directory);
        var path = WriteFile(directory, "a.mp3");

        RaiseCreated(directory, "a.mp3");

        // Reading it here is reading whatever the copy had written so far, and the row it makes is
        // keyed by the hash of that: half a file, indexed, and never looked at again.
        Assert.Empty(_seen);

        LetItSettle();

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Appeared, change.Kind);
        Assert.Equal(path, change.Path);
        Assert.Null(change.PreviousPath);
    }

    [Fact]
    public void AFileStillBeingWrittenTo_IsNotReportedUntilTheCopyStops()
    {
        // A copy onto a slow mount goes quiet between two writes, and the events it raises are not
        // enough on their own to say it is finished. The size is.
        var directory = Directory("/music");
        _sut.Watch(directory);
        var path = WriteFile(directory, "a.mp3", "aud");

        RaiseCreated(directory, "a.mp3");
        _fileSystem.File.AppendAllText(path, "io and rather more of it");

        // Quiet for the whole window and a different size than when the event arrived, so the
        // copy is still running and there is nothing worth reading yet.
        _scheduler.AdvanceBy(SettleFor + TimeSpan.FromMilliseconds(100));
        Assert.Empty(_seen);

        _scheduler.AdvanceBy(SettleFor + TimeSpan.FromMilliseconds(100));

        Assert.Equal(path, Assert.Single(_seen).Path);
    }

    [Fact]
    public void AFileWrittenInPlace_IsReported()
    {
        // Retagging a file in another application is a write and no rename, so Created never fires
        // and the library kept showing what the file said before it was edited.
        var directory = Directory("/music");
        _sut.Watch(directory);
        var path = WriteFile(directory, "a.mp3");

        RaiseChanged(directory, "a.mp3");

        LetItSettle();

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Appeared, change.Kind);
        Assert.Equal(path, change.Path);
    }

    [Fact]
    public void AWriteToADirectory_IsNotReportedAsAFile()
    {
        // Everything under the root reports its writes, directories included, and there is nothing
        // in one to read.
        var directory = Directory("/music");
        _sut.Watch(directory);
        Directory(_fileSystem.Path.Combine(directory.FullName, "album"));

        RaiseChanged(directory, "album");

        LetItSettle();

        Assert.Empty(_seen);
    }

    [Fact]
    public void Deleted_IsReportedAsVanished()
    {
        var directory = Directory("/music");
        _sut.Watch(directory);

        RaiseDeleted(directory, "a.mp3", NotifyFilters.FileName);

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Vanished, change.Kind);
    }

    [Fact]
    public void Renamed_CarriesBothEnds()
    {
        // The audio is unchanged across a rename, so the new path inherits everything approved
        // about the old one. A report that dropped the old path could not express that.
        var directory = Directory("/music");
        _sut.Watch(directory);

        RaiseRenamed(directory, "new.mp3", "old.mp3", NotifyFilters.FileName);

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Renamed, change.Kind);
        Assert.EndsWith("new.mp3", change.Path, StringComparison.Ordinal);
        Assert.EndsWith("old.mp3", change.PreviousPath!, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderThatWasRenamed_IsReportedWithBothEnds()
    {
        // Tidying a folder up in a file manager is the act this whole component exists for, and
        // it is one notification about a directory name: nothing under it is reported separately.
        var directory = Directory("/music");
        _sut.Watch(directory);
        var album = _fileSystem.Path.Combine(directory.FullName, "album");
        Directory(album);
        var renamed = _fileSystem.Path.Combine(directory.FullName, "Naragonia");
        _fileSystem.Directory.Move(album, renamed);

        RaiseRenamed(directory, "Naragonia", "album", NotifyFilters.DirectoryName);

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Renamed, change.Kind);
        Assert.Equal(renamed, change.Path);
        Assert.Equal(album, change.PreviousPath);
    }

    [Fact]
    public void AFolderThatWasDeleted_IsReportedAsVanished()
    {
        // Sending a folder to the recycle bin is a directory notification too, and everything the
        // library holds under it went with it.
        var directory = Directory("/music");
        _sut.Watch(directory);
        var album = _fileSystem.Path.Combine(directory.FullName, "album");
        Directory(album);
        _fileSystem.Directory.Delete(album, recursive: true);

        RaiseDeleted(directory, "album", NotifyFilters.DirectoryName);

        var change = Assert.Single(_seen);
        Assert.Equal(LibraryFileChangeKind.Vanished, change.Kind);
        Assert.Equal(album, change.Path);
    }

    [Fact]
    public async Task AWatcherThatFailed_IsStartedAgainAndSaysSo()
    {
        // A buffer overflow takes the events it could not hold with it and, on Windows, the
        // watcher with them. Nothing said so, and a library that quietly stopped noticing files
        // looks exactly like one nobody added anything to.
        var directory = Directory("/music");
        _sut.Watch(directory);

        Latest.Error += Raise.Event<ErrorEventHandler>(
            Latest, new ErrorEventArgs(new InternalBufferOverflowException()));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));

        Assert.Equal(2, _watchers.Count);
        Assert.True(Latest.EnableRaisingEvents);
        await _loggerService.Received().WarningAsync(Arg.Any<string>());

        // And it is reporting again, which is the whole point of starting it.
        WriteFile(directory, "a.mp3");
        RaiseCreated(directory, "a.mp3");
        LetItSettle();

        Assert.Single(_seen);
    }

    [Fact]
    public void AWatcherThatFailedWithItsDirectoryGone_KeepsAskingForIt()
    {
        // The drive was pulled. Giving up here is what left the DJ with a library that noticed
        // nothing more until the application was restarted.
        var directory = Directory("/music");
        _sut.Watch(directory);
        _fileSystem.Directory.Delete(directory.FullName, recursive: true);

        Latest.Error += Raise.Event<ErrorEventHandler>(
            Latest, new ErrorEventArgs(new InternalBufferOverflowException()));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));

        Assert.Single(_watchers);

        _scheduler.AdvanceBy(RetryEvery + TimeSpan.FromSeconds(1));
        Assert.Single(_watchers);

        _fileSystem.Directory.CreateDirectory(directory.FullName);
        _scheduler.AdvanceBy(RetryEvery + TimeSpan.FromSeconds(1));

        Assert.Equal(2, _watchers.Count);
        Assert.True(Latest.EnableRaisingEvents);
    }

    [Fact]
    public async Task AWatcherThatCannotBeOpenedOnARetry_IsAskedForAgainRatherThanThrowing()
    {
        // The mount is there for the check and gone again a moment later, which is exactly what a
        // flapping USB or SMB drive does. This runs on the scheduler with nobody above it to catch
        // anything, so a throw reaches the process-wide handler: an ERROR line in the log and the
        // application gone in front of the room, on the very path that exists to survive this.
        var directory = Directory("/music");
        _sut.Watch(directory);
        _watchersThatCannotBeOpened = 1;

        Latest.Error += Raise.Event<ErrorEventHandler>(
            Latest, new ErrorEventArgs(new InternalBufferOverflowException()));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));

        Assert.Single(_watchers);
        await _loggerService.Received().WarningAsync(Arg.Any<string>());

        _scheduler.AdvanceBy(RetryEvery + TimeSpan.FromSeconds(1));

        Assert.Equal(2, _watchers.Count);
        Assert.True(Latest.EnableRaisingEvents);

        // And reporting again, which is the whole point of asking for it again.
        WriteFile(directory, "a.mp3");
        RaiseCreated(directory, "a.mp3");
        LetItSettle();

        Assert.Single(_seen);
    }

    [Fact]
    public void Watch_Twice_LeavesOnlyTheNewerWatcherReporting()
    {
        // Switching the music directory used to leave the old subscriptions attached, so a file
        // touched under the directory nobody is using any more still reached the store.
        var first = Directory("/music/a");
        var second = Directory("/music/b");
        _sut.Watch(first);
        var stale = Latest;
        _sut.Watch(second);

        WriteFile(first, "stale.mp3");
        stale.Created += Raise.Event<FileSystemEventHandler>(
            stale, new FileSystemEventArgs(WatcherChangeTypes.Created, first.FullName, "stale.mp3"));
        LetItSettle();

        Assert.Empty(_seen);

        WriteFile(second, "fresh.mp3");
        RaiseCreated(second, "fresh.mp3");
        LetItSettle();

        Assert.Single(_seen);
    }

    [Fact]
    public void Stop_LeavesItUsableAgain()
    {
        var directory = Directory("/music");
        _sut.Watch(directory);
        _sut.Stop();

        _sut.Watch(directory);
        WriteFile(directory, "a.mp3");
        RaiseCreated(directory, "a.mp3");
        LetItSettle();

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
