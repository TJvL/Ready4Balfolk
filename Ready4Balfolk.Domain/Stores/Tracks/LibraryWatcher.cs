using System.IO.Abstractions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

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
public sealed class LibraryWatcher(IFileSystem fileSystem) : IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly Subject<LibraryFileChange> _changes = new();

    private IFileSystemWatcher? _watcher;
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

        // Local capture: the FromEventPattern remove-handlers must close over this instance, not
        // the field, which is replaced on the next reload before the old subscriptions are
        // disposed.
        var watcher = fileSystem.FileSystemWatcher.New(directory.FullName);
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.FileName;
        _watcher = watcher;

        _subscriptions.Add(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => watcher.Created += handler,
                handler => watcher.Created -= handler)
            .Subscribe(evt => Publish(new LibraryFileChange(
                LibraryFileChangeKind.Appeared, evt.EventArgs.FullPath))));

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

        watcher.EnableRaisingEvents = true;
    }

    /// <summary>Stops watching, leaving this usable again.</summary>
    public void Stop()
    {
        _subscriptions.Clear();
        _watcher?.Dispose();
        _watcher = null;
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
