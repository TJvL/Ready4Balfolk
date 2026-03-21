using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Tracks.Discovery;

namespace Ready4Balfolk.Tests.Unit;

public class DanceFileDiscoveryServiceTests
{
    [Fact]
    public void BasicWriteReadTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem));

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

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem));

        var readContent = dfd.Matches(directoryInfo);
        Assert.Empty(readContent);
    }

    [Fact]
    public void ReadNotExistingDirectoryTest()
    {
        var mockFileSystem = new MockFileSystem();

        var dir = mockFileSystem.DirectoryInfo.New("TestDirectory");
        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem));

        Assert.Throws<DirectoryNotFoundException>(() => dfd.Matches(dir));
    }
}
