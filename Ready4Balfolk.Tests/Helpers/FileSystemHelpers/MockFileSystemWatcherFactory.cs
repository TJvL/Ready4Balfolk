using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ready4Balfolk.Tests.Integration;

// The default Testing framework has no support for FileSystemWatchers, and this forces it
// Note: This is not a perfect mock, but good enough to function.
public class MockFileSystemWatcherFactory : IFileSystemWatcherFactory
{
    private readonly Func<string, IFileSystemWatcher> _watcher;

    public MockFileSystemWatcherFactory(MockFileSystem mockFileSystem, Func<string, IFileSystemWatcher> watcher)
    {
        _watcher = watcher;
        FileSystem = mockFileSystem;
    }

    public IFileSystem FileSystem { get; }

    public IFileSystemWatcher New() => throw new NotSupportedException();

    public IFileSystemWatcher New(string path) => _watcher(path);

    public IFileSystemWatcher New(string path, string filter) => throw new NotSupportedException();

    public IFileSystemWatcher Wrap(FileSystemWatcher fileSystemWatcher)
    {
        if (fileSystemWatcher == null)
        {
            return null;
        }

        throw new NotSupportedException();
    }
}
