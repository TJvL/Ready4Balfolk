using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Ready4Balfolk.Tests.Helpers.FileSystemHelpers;

public class WatchableMockFileSystem(Func<string, IFileSystemWatcher> watcher) : MockFileSystem
{
    public override IFileSystemWatcherFactory FileSystemWatcher { get; } = new MockFileSystemWatcherFactory(new MockFileSystem(), watcher);
}
