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
    private static readonly string SettingsPath = Path.Combine(Root, "settings.json");

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
        Assert.True(fileSystem.File.Exists(SettingsPath));
        Assert.Contains("42", await fileSystem.File.ReadAllTextAsync(SettingsPath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
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

        Assert.False(fileSystem.File.Exists(SettingsPath + ".tmp"));
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

        Assert.DoesNotContain(SettingsPath, created);
        Assert.Contains(SettingsPath + ".tmp", created);
        Assert.Contains(SettingsPath, moved);
    }

    [Fact]
    public void Current_CorruptFile_FallsBackToDefaultsRatherThanThrowing()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(Root);
        fileSystem.File.WriteAllText(SettingsPath, "{ this is not json");

        var (store, _) = Create(fileSystem);

        // Starting with defaults beats refusing to start at all, in front of a room.
        Assert.Equal(new ApplicationSettings(), store.Current);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentWriters_AllLandAndTheFileStaysReadable()
    {
        var (store, fileSystem) = Create();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            store.UpdateAsync(s => s with { MaxQueueItems = i })));

        var text = await fileSystem.File.ReadAllTextAsync(SettingsPath, TestContext.Current.CancellationToken);
        Assert.Contains("\"maxQueueItems\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(fileSystem.File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void Dispose_IsSafe()
    {
        var (store, _) = Create();

        store.Dispose();
    }
}
