using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Models.Tracks;
using Ready4Balfolk.Domain.Services.Logging;
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

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

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

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

        var readContent = dfd.Matches(directoryInfo);
        Assert.Empty(readContent);
    }

    [Fact]
    public void ReadNotExistingDirectoryTest()
    {
        var mockFileSystem = new MockFileSystem();

        var dir = mockFileSystem.DirectoryInfo.New("TestDirectory");
        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

        Assert.Throws<DirectoryNotFoundException>(() => dfd.Matches(dir));
    }

    [Fact]
    public void RereadAfterDanceFileChangedTest()
    {
        // A stale cache entry used to throw ArgumentException from Dictionary.Add.
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

        dfd.Write(directoryInfo, [new DanceFileEntry("Test 1", "Dance 1")]);
        _ = dfd.Matches(directoryInfo);

        dfd.Write(directoryInfo, [new DanceFileEntry("Test 1", "Dance 2")]);
        var dancesPath = mockFileSystem.Path.Combine(directoryInfo.FullName, "dances.json");
        mockFileSystem.File.SetLastWriteTimeUtc(dancesPath, DateTime.UtcNow.AddMinutes(1));

        var readContent = dfd.Matches(directoryInfo);

        var item = Assert.Single(readContent);
        Assert.Equal("Dance 2", item.Value);
    }

    [Fact]
    public void MalformedDanceFileReturnsEmptyTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();
        mockFileSystem.AddFile(
            mockFileSystem.Path.Combine(directoryInfo.FullName, "dances.json"),
            new MockFileData("this is not json"));

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

        var readContent = dfd.Matches(directoryInfo);

        Assert.Empty(readContent);
    }

    [Fact]
    public void DuplicateEntriesFirstWinsTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));
        dfd.Write(directoryInfo,
        [
            new DanceFileEntry("Test 1", "Dance 1"),
            new DanceFileEntry("Test 1", "Dance 2")
        ]);

        var readContent = dfd.Matches(directoryInfo);

        var item = Assert.Single(readContent);
        Assert.Equal("Dance 1", item.Value);
    }

    [Fact]
    public void MatchesAreCaseInsensitiveTest()
    {
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));
        dfd.Write(directoryInfo, [new DanceFileEntry("Test 1.mp3", "Dance 1")]);

        var readContent = dfd.Matches(directoryInfo);

        Assert.True(readContent.TryGetValue("TEST 1.MP3", out var dance));
        Assert.Equal("Dance 1", dance);
    }

    [Fact]
    public void EmptyDanceFileWritesTemplateTest()
    {
        // The empty-file rewrite used to happen while the read stream was still
        // open, which fails under Windows-style sharing rules (and MockFileSystem).
        var mockFileSystem = new MockFileSystem();

        var directoryInfo = mockFileSystem.DirectoryInfo.New("TestDirectory");
        directoryInfo.Create();
        mockFileSystem.AddFile(
            mockFileSystem.Path.Combine(directoryInfo.FullName, "Song.mp3"),
            new MockFileData(""));
        var dancesPath = mockFileSystem.Path.Combine(directoryInfo.FullName, "dances.json");
        mockFileSystem.AddFile(dancesPath, new MockFileData("[]"));

        var dfd = new DanceFileDiscoveryService(new DanceFileService(mockFileSystem, new NoOpLoggerService()));

        var readContent = dfd.Matches(directoryInfo);

        Assert.Empty(readContent);
        Assert.Contains("Song.mp3", mockFileSystem.File.ReadAllText(dancesPath));
    }
}
