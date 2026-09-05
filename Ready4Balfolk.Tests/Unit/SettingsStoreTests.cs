using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Stores;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// Everything the application remembers between runs.
/// </summary>
/// <remarks>
/// Untestable until the store stopped reaching for <c>System.IO</c> directly, which is why it had
/// no tests despite being the thing a corrupt write costs the most.
/// </remarks>
public sealed class SettingsStoreTests
{
    private const string Root = "/data";

    /// <summary>
    /// Where the store will actually write, asked of the filesystem rather than assumed.
    /// </summary>
    /// <remarks>
    /// MockFileSystem normalises a rooted path per platform, so "/data" is `C:\data` on Windows.
    /// A hard coded constant matches on Linux and fails on Windows, which is what the two platform
    /// matrix is for.
    /// </remarks>
    private static string SettingsPathIn(MockFileSystem fileSystem) =>
        Path.Combine(fileSystem.DirectoryInfo.New(Root).FullName, "settings.json");

    private static (SettingsStore Store, MockFileSystem FileSystem) Create(MockFileSystem? fileSystem = null)
    {
        var system = fileSystem ?? new MockFileSystem();
        system.Directory.CreateDirectory(Root);

        var directory = Substitute.For<IApplicationSettingsDirectory>();
        directory.DirectoryInfoRoot.Returns(_ => system.DirectoryInfo.New(Root));

        return (new SettingsStore(directory, system, new NoOpLoggerService()), system);
    }

    [Fact]
    public void Current_NoFileYet_IsTheDefaults()
    {
        var (store, _) = Create();

        Assert.Equal(new ApplicationSettings(), store.Current);
    }

    [Fact]
    public async Task UpdateAsync_PublishesAndPersists()
    {
        var (store, fileSystem) = Create();

        await store.UpdateAsync(s => s with { MaxQueueItems = 42 });

        Assert.Equal(42, store.Current.MaxQueueItems);
        Assert.True(fileSystem.File.Exists(SettingsPathIn(fileSystem)));
        Assert.Contains("42", await fileSystem.File.ReadAllTextAsync(SettingsPathIn(fileSystem), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_NotifiesSubscribers()
    {
        var (store, _) = Create();
        var seen = new List<int>();
        using var subscription = store.Observe().Subscribe(s => seen.Add(s.MaxQueueItems));

        await store.UpdateAsync(s => s with { MaxQueueItems = 7 });

        // The replay of the current value, then the change.
        Assert.Equal([new ApplicationSettings().MaxQueueItems, 7], seen);
    }

    [Fact]
    public async Task NewStore_ReadsWhatTheLastOneWrote()
    {
        var (store, fileSystem) = Create();
        await store.UpdateAsync(s => s with { MaxQueueItems = 13, ShowButtonText = true });

        var (reopened, _) = Create(fileSystem);

        Assert.Equal(13, reopened.Current.MaxQueueItems);
        Assert.True(reopened.Current.ShowButtonText);
    }

    [Fact]
    public async Task SaveAsync_LeavesNoTemporaryFileBehind()
    {
        var (store, fileSystem) = Create();

        await store.UpdateAsync(s => s with { MaxQueueItems = 5 });

        Assert.False(fileSystem.File.Exists(SettingsPathIn(fileSystem) + ".tmp"));
    }

    /// <summary>The real settings file is never opened for writing.</summary>
    /// <remarks>
    /// This is the atomic write, stated as something a test can see. `File.Create` truncates before
    /// it writes, so a crash part way through left a half written file, and loading treats a file it
    /// cannot parse as absent: the visible symptom was every setting silently back to its factory
    /// value. Serialising into a temporary file and moving it over the real one means the real path
    /// only ever changes in one step.
    ///
    /// A crash mid-write cannot be staged against MockFileSystem, so what is asserted instead is the
    /// mechanism: the real path reaches Move and never reaches Create.
    /// </remarks>
    [Fact]
    public async Task SaveAsync_TheRealFileIsOnlyEverMovedIntoPlace()
    {
        var mock = new MockFileSystem();
        mock.Directory.CreateDirectory(Root);

        var created = new List<string>();
        var moved = new List<string>();

        var file = Substitute.For<IFile>();
        file.Exists(Arg.Any<string>()).Returns(call => mock.File.Exists(call.Arg<string>()));
        file.Create(Arg.Any<string>()).Returns(call =>
        {
            created.Add(call.Arg<string>());
            return mock.File.Create(call.Arg<string>());
        });
        file.When(f => f.Move(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>()))
            .Do(call =>
            {
                moved.Add(call.ArgAt<string>(1));
                mock.File.Move(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<bool>(2));
            });

        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Returns(file);
        fileSystem.FileStream.Returns(mock.FileStream);
        fileSystem.DirectoryInfo.Returns(mock.DirectoryInfo);

        var directory = Substitute.For<IApplicationSettingsDirectory>();
        directory.DirectoryInfoRoot.Returns(_ => mock.DirectoryInfo.New(Root));

        using var store = new SettingsStore(directory, fileSystem, new NoOpLoggerService());
        await store.UpdateAsync(s => s with { MaxQueueItems = 9 });

        var expected = SettingsPathIn(mock);
        Assert.DoesNotContain(expected, created);
        Assert.Contains(expected + ".tmp", created);
        Assert.Contains(expected, moved);
    }

    [Fact]
    public void Current_CorruptFile_FallsBackToDefaultsRatherThanThrowing()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(SettingsPathIn(fileSystem), "{ this is not json");

        var (store, _) = Create(fileSystem);

        // Starting with defaults beats refusing to start at all, in front of a room.
        Assert.Equal(new ApplicationSettings(), store.Current);
    }

    /// <summary>An unreadable file is kept, not written over on the next save.</summary>
    /// <remarks>
    /// It is a file the user is invited to edit by hand, and the only copy of what they had, so it
    /// is moved aside under a fixed name they can be pointed at.
    /// </remarks>
    [Fact]
    public void Current_CorruptFile_IsKeptBesideTheRealOne()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(SettingsPathIn(fileSystem), "{ this is not json");

        var (_, _) = Create(fileSystem);

        Assert.False(fileSystem.File.Exists(SettingsPathIn(fileSystem)));
        Assert.Equal(
            "{ this is not json",
            fileSystem.File.ReadAllText(SettingsPathIn(fileSystem) + ".corrupt"));
    }

    /// <summary>A second bad start does not throw over the file the first one kept.</summary>
    [Fact]
    public void Current_CorruptFileTwice_KeepsTheLatestUnderTheOneName()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(SettingsPathIn(fileSystem), "first");
        Create(fileSystem);
        fileSystem.File.WriteAllText(SettingsPathIn(fileSystem), "second");

        var (store, _) = Create(fileSystem);

        Assert.Equal(new ApplicationSettings(), store.Current);
        Assert.Equal("second", fileSystem.File.ReadAllText(SettingsPathIn(fileSystem) + ".corrupt"));
    }

    /// <summary>An enum member this build has never heard of costs that field and nothing else.</summary>
    [Fact]
    public void Current_UnknownEnumMember_KeepsTheRestOfTheFile()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(
            SettingsPathIn(fileSystem),
            """{"MaxQueueItems":11,"SetupCompleted":true,"ApplicationTheme":"Chartreuse"}""");

        var (store, _) = Create(fileSystem);

        Assert.Equal(11, store.Current.MaxQueueItems);
        Assert.True(store.Current.SetupCompleted);
        Assert.True(fileSystem.File.Exists(SettingsPathIn(fileSystem)));
    }

    /// <summary>A field written by some other build is skipped rather than taken as corruption.</summary>
    [Fact]
    public void Current_UnknownField_KeepsTheRestOfTheFile()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(
            SettingsPathIn(fileSystem),
            """{"MaxQueueItems":11,"SomethingThisBuildNeverHeardOf":"yes"}""");

        var (store, _) = Create(fileSystem);

        Assert.Equal(11, store.Current.MaxQueueItems);
    }

    /// <summary>A file another process is holding open does not take the application down with it.</summary>
    /// <remarks>
    /// The load runs in the constructor, during DI composition, so an IOException escaping it is
    /// not a settings problem: it is the application never reaching a window.
    /// </remarks>
    [Fact]
    public void Current_FileHeldOpenByAnotherProcess_StartsFromDefaultsAndLeavesItAlone()
    {
        var mock = new MockFileSystem();
        mock.Directory.CreateDirectory(Root);
        mock.File.WriteAllText(SettingsPathIn(mock), "{}");

        var streams = Substitute.For<IFileStreamFactory>();
        streams.New(Arg.Any<string>(), Arg.Any<FileMode>(), Arg.Any<FileAccess>())
            .Returns(_ => throw new IOException("held open by another process"));

        var fileSystem = Substitute.For<IFileSystem>();
        fileSystem.File.Returns(mock.File);
        fileSystem.FileStream.Returns(streams);
        fileSystem.DirectoryInfo.Returns(mock.DirectoryInfo);

        var directory = Substitute.For<IApplicationSettingsDirectory>();
        directory.DirectoryInfoRoot.Returns(_ => mock.DirectoryInfo.New(Root));

        using var store = new SettingsStore(directory, fileSystem, new NoOpLoggerService());

        Assert.Equal(new ApplicationSettings(), store.Current);
        // Unreadable now is not the same as unreadable content: the file may be perfectly good.
        Assert.True(mock.File.Exists(SettingsPathIn(mock)));
        Assert.False(mock.File.Exists(SettingsPathIn(mock) + ".corrupt"));
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentWriters_AllLandAndTheFileStaysReadable()
    {
        var (store, fileSystem) = Create();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            store.UpdateAsync(s => s with { MaxQueueItems = i })));

        var text = await fileSystem.File.ReadAllTextAsync(SettingsPathIn(fileSystem), TestContext.Current.CancellationToken);
        Assert.Contains("\"maxQueueItems\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(fileSystem.File.Exists(SettingsPathIn(fileSystem) + ".tmp"));
    }

    [Fact]
    public void Dispose_IsSafe()
    {
        var (store, _) = Create();

        store.Dispose();
    }
}
