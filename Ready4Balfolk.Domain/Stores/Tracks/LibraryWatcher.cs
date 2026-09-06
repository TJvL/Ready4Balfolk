using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Domain.Stores.Tracks;

/// <summary>What happened to a file under the music directory.</summary>
/// <remarks>
/// <see cref="PreviousPath"/> is set only for a rename, where both ends matter: the audio is
/// unchanged, so the new path inherits everything that was approved about the old one.
/// </remarks>
public sealed record LibraryFileChange(LibraryFileChangeKind Kind, string Path, string? PreviousPath = null);

public enum LibraryFileChangeKind
{
    Appeared,
    Vanished,
    Renamed
}

/// <summary>
/// Watches the music directory and says what changed. It decides nothing about what that means.
/// </summary>
/// <remarks>
/// This was the watcher lifetime, three event subscriptions and their disposal, living inside
/// TrackStore alongside the scanning and publishing. Separating noticing from reacting is what lets
/// either be read on its own, and the store keeps every decision about what a change is worth.
/// </remarks>
/// <param name="fileSystem">Where the watcher is opened and where a settling file is measured.</param>
/// <param name="loggerService">Where a watcher that fell over says so.</param>
/// <param name="settleFor">
/// How long a path has to hold still before what happened to it is reported, or the default.
/// </param>
/// <param name="retryEvery">
/// How often a watcher that fell over asks for a directory that is not there back, or the default.
/// </param>
/// <param name="scheduler">Where the settling and the retries are timed, or the default one.</param>
public sealed class LibraryWatcher(
    IFileSystem fileSystem,
    ILoggerService loggerService,
    TimeSpan? settleFor = null,
    TimeSpan? retryEvery = null,
    IScheduler? scheduler = null) : IDisposable
{
    /// <summary>How long a path has to hold still before it is reported.</summary>
    public static readonly TimeSpan DefaultSettleFor = TimeSpan.FromSeconds(1);

    /// <summary>How often a watcher that fell over asks for a directory that is gone back.</summary>
    public static readonly TimeSpan DefaultRetryEvery = TimeSpan.FromSeconds(10);

    // The default is 8 KB, which an album's worth of files fills before anything has read it. On
    // Windows this is the buffer ReadDirectoryChangesW writes into, and raising it is the whole of
    // the fix there. On Linux it only sizes the read of the inotify queue, whose own limit is a
    // sysctl no process can raise for itself, so an overflow is still possible: Error is what
    // covers both, since neither platform reports one anywhere else.
    private const int InternalBufferBytes = 64 * 1024;

    private readonly TimeSpan _settleFor = settleFor ?? DefaultSettleFor;
    private readonly TimeSpan _retryEvery = retryEvery ?? DefaultRetryEvery;
    private readonly IScheduler _scheduler = scheduler ?? DefaultScheduler.Instance;
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Subject<LibraryFileChange> _changes = new();

    private IFileSystemWatcher? _watcher;
    private string? _root;
    // Which watcher a scheduled second look belongs to. A settling file is re-read on the
    // scheduler rather than through the subscriptions, so this is what keeps one that was armed
    // before a directory switch from reporting a file nobody is watching any more.
    private object _generation = new();
    private bool _disposed;

    /// <summary>Everything the watcher notices, unfiltered.</summary>
    public IObservable<LibraryFileChange> Changes => _changes.AsObservable();

    /// <summary>Starts watching a directory, replacing whatever was being watched before.</summary>
    /// <remarks>
    /// Does nothing once disposed: enabling a disposed watcher is what threw
    /// ObjectDisposedException, and a watcher nobody will dispose is worse than none.
    /// </remarks>
    public void Watch(IFileSystemInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (_disposed)
        {
            return;
        }

        Stop();
        _root = directory.FullName;
        Start(directory.FullName);
    }

    /// <summary>Stops watching, leaving this usable again.</summary>
    public void Stop()
    {
        _subscriptions.Clear();
        _watcher?.Dispose();
        _watcher = null;
        _root = null;
        // Anything already waiting for a file to hold still belonged to the watcher that is going.
        _generation = new object();
    }

    /// <summary>A path an event arrived for, and what the file looked like at that moment.</summary>
    private sealed record Touched(string Path, long Length, DateTime LastWriteUtc);

    private void Start(string root)
    {
        // Local capture: the FromEventPattern remove-handlers must close over this instance, not
        // the field, which is replaced on the next reload before the old subscriptions are
        // disposed.
        var watcher = fileSystem.FileSystemWatcher.New(root);
        watcher.IncludeSubdirectories = true;
        // DirectoryName as well as FileName, or a folder being renamed, moved or sent to the
        // recycle bin raises nothing at all on Windows: ReadDirectoryChangesW is told which kinds
        // of name to report, FileName means files and DirectoryName means folders. The inotify
        // backend does not separate the two, so leaving it out breaks the DJ's machine and not the
        // one it was written on.
        //
        // LastWrite and Size as well. Created fires when the file is made and the copy fills it
        // afterwards, so on names alone there is never a second look and the store reads whatever
        // the first few kilobytes happened to be. Size is there because Windows can hold a growing
        // file's write time back until it is closed.
        watcher.NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size;
        watcher.InternalBufferSize = InternalBufferBytes;
        _watcher = watcher;

        var touched = new Subject<Touched>();
        var generation = new object();
        _generation = generation;

        // One report per path per burst, and only once the file has stopped changing.
        _subscriptions.Add(touched
            .GroupByUntil(entry => entry.Path, group => group.Throttle(_settleFor, _scheduler))
            .SelectMany(group => group.TakeLast(1))
            .Subscribe(entry => ReportWhenStill(entry, generation)));

        _subscriptions.Add(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => watcher.Created += handler,
                handler => watcher.Created -= handler)
            .Subscribe(evt => touched.OnNext(Reading(evt.EventArgs.FullPath))));

        _subscriptions.Add(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => watcher.Changed += handler,
                handler => watcher.Changed -= handler)
            .Subscribe(evt => touched.OnNext(Reading(evt.EventArgs.FullPath))));

        _subscriptions.Add(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => watcher.Deleted += handler,
                handler => watcher.Deleted -= handler)
            .Subscribe(evt => Publish(new LibraryFileChange(
                LibraryFileChangeKind.Vanished, evt.EventArgs.FullPath))));

        _subscriptions.Add(Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                handler => watcher.Renamed += handler,
                handler => watcher.Renamed -= handler)
            .Subscribe(evt => Publish(new LibraryFileChange(
                LibraryFileChangeKind.Renamed, evt.EventArgs.FullPath, evt.EventArgs.OldFullPath))));

        _subscriptions.Add(Observable.FromEventPattern<ErrorEventHandler, ErrorEventArgs>(
                handler => watcher.Error += handler,
                handler => watcher.Error -= handler)
            .Subscribe(evt => OnFailed(root, evt.EventArgs.GetException())));

        watcher.EnableRaisingEvents = true;
    }

    private Touched Reading(string path)
    {
        var file = fileSystem.FileInfo.New(path);

        // A path that is not a file yet reads as nothing, which is never what is found at the end
        // of the quiet window: the first look is only ever a baseline to compare against.
        return file.Exists
            ? new Touched(path, file.Length, file.LastWriteTimeUtc)
            : new Touched(path, -1, DateTime.MinValue);
    }

    private void ReportWhenStill(Touched entry, object generation)
    {
        if (_disposed || !ReferenceEquals(_generation, generation))
        {
            return;
        }

        var file = fileSystem.FileInfo.New(entry.Path);
        if (!file.Exists)
        {
            // Gone again inside the quiet window, or never a file at all: a directory reports a
            // write like anything else under the root, and there is nothing to read in one.
            return;
        }

        if (file.Length != entry.Length || file.LastWriteTimeUtc != entry.LastWriteUtc)
        {
            // Quiet and still growing. A copy onto a slow mount goes quiet between two writes, so
            // the size is what says it is finished rather than the silence. Looked at again on the
            // scheduler rather than pushed back through the burst pipeline, which drops an element
            // that arrives while the path's group is expiring.
            var again = Reading(entry.Path);
            _ = _scheduler.Schedule(_settleFor, () => ReportWhenStill(again, generation));
            return;
        }

        Publish(new LibraryFileChange(LibraryFileChangeKind.Appeared, entry.Path));
    }

    /// <summary>Picks the watcher up again after it fell over, and says that it did.</summary>
    /// <remarks>
    /// A buffer overflow takes the events it could not hold with it and, on Windows, the watcher
    /// with them. Nothing said so: the library simply stopped noticing files, which from the far
    /// side of the room looks exactly like a library nobody added anything to.
    /// </remarks>
    private void OnFailed(string root, Exception exception)
    {
        _ = loggerService.WarningAsync(
            $"The watcher on '{root}' failed and is being started again: {exception.Message}");

        // Off the watcher's own callback, because starting again disposes the watcher that is
        // raising this.
        _subscriptions.Add(_scheduler.Schedule(() => Restart(root)));
    }

    private void Restart(string root)
    {
        if (_disposed || !string.Equals(_root, root, StringComparison.Ordinal))
        {
            // Watching somewhere else by now, or on the way out.
            return;
        }

        Stop();
        _root = root;

        if (fileSystem.Directory.Exists(root) && TryStart(root))
        {
            return;
        }

        // A drive that was pulled. Asked for again rather than given up on, which is what left the
        // DJ with a library that noticed nothing more until the application was restarted.
        _subscriptions.Add(_scheduler.Schedule(_retryEvery, () => Restart(root)));
    }

    /// <summary>Starts watching, or says it could not and leaves nothing half-attached.</summary>
    /// <remarks>
    /// A pulled mount comes and goes, so the directory can be there for the check and gone again
    /// by the time the watcher is opened on it: the factory throws ArgumentException for a
    /// directory that is not there, and enabling one throws FileNotFoundException. This runs on a
    /// scheduler thread with nobody above it to catch anything, so a throw here reaches the
    /// process-wide handler, which writes an ERROR line and ends the evening in front of the room.
    /// </remarks>
    private bool TryStart(string root)
    {
        try
        {
            Start(root);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            _ = loggerService.WarningAsync(
                $"The watcher on '{root}' could not be started and will be tried again: {exception.Message}");

            // Whatever Start managed to attach before it threw belongs to a watcher that is not
            // running. Stop clears the root as well, and the retry only fires while it is set.
            Stop();
            _root = root;
            return false;
        }
    }

    private void Publish(LibraryFileChange change)
    {
        if (!_disposed)
        {
            _changes.OnNext(change);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _subscriptions.Dispose();
        _changes.Dispose();
    }
}
