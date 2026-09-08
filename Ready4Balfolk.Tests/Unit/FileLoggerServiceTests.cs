using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>What the export button actually hands over.</summary>
/// <remarks>
/// The bug template asks for the exported file in a public issue, so this is the one thing in the
/// application that goes to a stranger by design.
/// </remarks>
public sealed class FileLoggerServiceTests : IDisposable
{
    private const int PastTheRotationBoundary = 600 * 1024;

    private readonly IDirectoryInfo _directory;

    public FileLoggerServiceTests()
    {
        _directory = new FileSystem().DirectoryInfo.New(
            Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}"));
    }

    [Fact]
    public async Task ExportAsync_CarriesBothSidesOfTheRotation()
    {
        using var logger = new FileLoggerService(_directory);

        // Half a megabyte fills in an hour of an evening, and the failure somebody exports the log
        // for is as often as not older than that. Rotating used to delete it outright.
        await logger.InfoAsync("the line that used to be lost" + new string('x', PastTheRotationBoundary));
        await logger.InfoAsync("the line that prompted the export");

        var text = await ExportedTextAsync(logger);

        Assert.Contains("the line that used to be lost", text, StringComparison.Ordinal);
        Assert.Contains("the line that prompted the export", text, StringComparison.Ordinal);
        Assert.True(
            text.IndexOf("the line that used to be lost", StringComparison.Ordinal)
            < text.IndexOf("the line that prompted the export", StringComparison.Ordinal),
            "The export should read as one run of time, oldest first.");
    }

    [Fact]
    public async Task ExportAsync_WithNothingLoggedYet_WritesNothing()
    {
        using var logger = new FileLoggerService(_directory);

        var destination = Path.Combine(_directory.FullName, "exported.log");
        await logger.ExportAsync(destination);

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ExportAsync_TakesTheUserProfileOutOfWhatLeavesTheMachine()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.SkipWhen(profile.Length == 0, "This machine has no user profile directory to take out.");

        using var logger = new FileLoggerService(_directory);
        var path = Path.Combine(profile, "Music", "a.mp3");
        await logger.ErrorAsync($"Error loading {path}: no such file");

        var text = await ExportedTextAsync(logger);

        Assert.DoesNotContain(profile, text, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("~", "Music", "a.mp3"), text, StringComparison.Ordinal);
        Assert.Contains("no such file", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_LeavesTheLogOnDiskExactlyAsItWasWritten()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.SkipWhen(profile.Length == 0, "This machine has no user profile directory to take out.");

        using var logger = new FileLoggerService(_directory);
        var path = Path.Combine(profile, "Music", "a.mp3");
        await logger.ErrorAsync($"Error loading {path}: no such file");

        await ExportedTextAsync(logger);

        // There is nobody to hide it from on the machine the log is about, and the DJ chasing this
        // themselves wants the real path.
        var onDisk = await File.ReadAllTextAsync(
            Path.Combine(_directory.FullName, "app.log"), TestContext.Current.CancellationToken);
        Assert.Contains(path, onDisk, StringComparison.Ordinal);
    }

    /// <summary>A logger built on a fake file system must never fall back to the real disk.</summary>
    /// <remarks>
    /// The constructor takes the directory apart to build <c>_logFile</c>, but a write that goes
    /// through the static <see cref="File"/> instead of <c>_logFile.FileSystem.File</c>
    /// lands on whatever file system that static class actually is: the real one, regardless of what
    /// was injected.
    /// </remarks>
    [Fact]
    public async Task LogAsync_WritesThroughTheInjectedFileSystemNotTheRealDisk()
    {
        var mock = new MockFileSystem();
        var directoryPath = Path.Combine(Path.GetTempPath(), $"r4b_test_{Guid.NewGuid():N}");
        mock.Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(directoryPath);
        try
        {
            var directory = mock.DirectoryInfo.New(directoryPath);
            using var logger = new FileLoggerService(directory);

            await logger.InfoAsync("written through the mock");

            var logPath = Path.Combine(directoryPath, "app.log");
            Assert.True(mock.File.Exists(logPath));
            Assert.Contains(
                "written through the mock", mock.File.ReadAllText(logPath), StringComparison.Ordinal);
            Assert.False(
                File.Exists(logPath),
                "A logger given a fake file system must never touch the real disk.");
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    public void Dispose()
    {
        _directory.Refresh();
        if (_directory.Exists)
        {
            _directory.Delete(recursive: true);
        }
    }

    private async Task<string> ExportedTextAsync(FileLoggerService logger)
    {
        var destination = Path.Combine(_directory.FullName, "exported.log");
        await logger.ExportAsync(destination);
        return await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken);
    }
}
