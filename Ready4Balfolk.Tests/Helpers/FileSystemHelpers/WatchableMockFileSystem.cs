using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

public class WatchableMockFileSystem : MockFileSystem
{
    // Built in the constructor rather than in an initializer, which cannot reach this: a factory
    // whose FileSystem is a different, empty mock would answer anything read through it with none of
    // the files the test put here.
    public WatchableMockFileSystem(Func<string, IFileSystemWatcher> watcher)
    {
        FileSystemWatcher = new MockFileSystemWatcherFactory(this, watcher);
    }

    public override IFileSystemWatcherFactory FileSystemWatcher { get; }
}
