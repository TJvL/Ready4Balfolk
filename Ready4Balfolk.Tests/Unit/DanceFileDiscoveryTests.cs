using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Services.Tracks;

namespace Ready4Balfolk.Tests.Unit;

public class DanceFileDiscoveryTests
{
    [Fact]
    public void BasicWriteReadTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscovery(mockFileSystem);

        ICollection<DanceFileEntry> content = [new DanceFileEntry("Test 1", "Dance 1")];
        dfd.Write(directoryInfo, content);

        var readContent = dfd.Matches(directoryInfo);
        var item = Assert.Single(readContent);
        Assert.Multiple(
            () => Assert.Equal("Test 1", item.Key),
            () => Assert.Equal("Dance 1", item.Value)
        );
    }

    [Fact]
    public void ReadNotAvailableTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscovery(mockFileSystem);

        var readContent = dfd.Matches(directoryInfo);
        Assert.Empty(readContent);
    }

    [Fact]
    public void ReadNotExistingDirectoryTest()
    {
        var mockFileSystem = new MockFileSystem();

        var dir = mockFileSystem.DirectoryInfo.New("TestDirectory");
        var dfd = new DanceFileDiscovery(mockFileSystem);

        Assert.Throws<DirectoryNotFoundException>(() => dfd.Matches(dir));
    }
}
