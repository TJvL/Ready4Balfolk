using Ready4Balfolk.UI.Platform;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>What a language change starts, on each platform.</summary>
/// <remarks>
/// The settings tests replace the restart, because the real one ends in an exit. This is the part of
/// it that can be read without ending anything: the restart was Linux-only for as long as nothing
/// looked at it.
/// </remarks>
public sealed class ApplicationRestartTests
{
    private static readonly string ExePath = Path.Combine(Path.GetTempPath(), "Ready 4 Balfolk", "Ready4Balfolk");

    [Fact]
    public void OnWindows_StartsTheApplicationThroughCmdRatherThanSetsid()
    {
        var start = ApplicationRestart.StartInfo(ExePath, onWindows: true);

        Assert.Equal("cmd.exe", start.FileName);
        Assert.DoesNotContain("setsid", start.Arguments, StringComparison.Ordinal);
        Assert.EndsWith($"start \"\" \"{ExePath}\"", start.Arguments, StringComparison.Ordinal);
        Assert.True(start.CreateNoWindow, "A console window would flash up in front of the room.");
    }

    [Fact]
    public void OnWindows_WaitsBeforeStarting()
    {
        var start = ApplicationRestart.StartInfo(ExePath, onWindows: true);

        Assert.StartsWith("/c ping -n 2 127.0.0.1 >nul & ", start.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void ElsewhereStartsANewSessionThroughSetsid()
    {
        var start = ApplicationRestart.StartInfo(ExePath, onWindows: false);

        Assert.Equal("setsid", start.FileName);
        Assert.Equal($"--fork /bin/sh -c \"sleep 0.3 && '{ExePath}'\"", start.Arguments);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartsInTheApplicationsOwnDirectory(bool onWindows) =>
        Assert.Equal(
            Path.GetDirectoryName(ExePath),
            ApplicationRestart.StartInfo(ExePath, onWindows).WorkingDirectory);
}
