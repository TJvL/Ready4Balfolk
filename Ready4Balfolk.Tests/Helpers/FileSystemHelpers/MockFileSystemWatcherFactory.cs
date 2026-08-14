using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

// The default Testing framework has no support for FileSystemWatchers, and this forces it
// Note: This is not a perfect mock, but good enough to function.
public class MockFileSystemWatcherFactory(MockFileSystem mockFileSystem, Func<string, IFileSystemWatcher> watcher)
    : IFileSystemWatcherFactory
{
    public IFileSystem FileSystem { get; } = mockFileSystem;

    public IFileSystemWatcher New() => throw new NotSupportedException();

    public IFileSystemWatcher New(string path) => watcher(path);

    public IFileSystemWatcher New(string path, string filter) => throw new NotSupportedException();

    public IFileSystemWatcher? Wrap(FileSystemWatcher? fileSystemWatcher) => throw new NotSupportedException();
}
