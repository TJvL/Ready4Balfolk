using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ready4Balfolk.Tests.Integration;

public class WatchableMockFileSystem : MockFileSystem
{
    public WatchableMockFileSystem(Func<string, IFileSystemWatcher> watcher) : base()
    {
        FileSystemWatcher = new MockFileSystemWatcherFactory(new MockFileSystem(), watcher);
    }

    public override IFileSystemWatcherFactory FileSystemWatcher { get; }
}