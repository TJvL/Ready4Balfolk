using Ready4Balfolk.Domain.Services.Logging;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>Saying which file, without saying whose disk it is on.</summary>
public sealed class LogPathsTests
{
    [Fact]
    public void Name_IsTheFileWithoutAnythingAboveIt()
    {
        Assert.Equal("a.mp3", LogPaths.Name(Path.Combine("music", "Mazurka", "a.mp3")));
        Assert.Equal("Mazurka", LogPaths.Name(Path.Combine("music", "Mazurka")));
        Assert.Equal("", LogPaths.Name(null));
        Assert.Equal("", LogPaths.Name(""));
    }

    [Fact]
    public void Name_OfAFolderWrittenWithATrailingSeparator_IsStillTheFolder()
    {
        var withSeparator = Path.Combine("music", "Mazurka") + Path.DirectorySeparatorChar;

        Assert.Equal("Mazurka", LogPaths.Name(withSeparator));
    }

    [Fact]
    public void Name_OfAVolumeRoot_IsStillTheRoot()
    {
        // A drive is a legitimate music directory, and it is the "not mounted yet" warning that
        // reads it back. There is nothing above a root to drop, and a warning about '' has lost
        // the only thing it had to say. A drive letter is not somebody's name or their disk layout.
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrEmpty(root));

        Assert.Equal(Path.TrimEndingDirectorySeparator(root), LogPaths.Name(root));
        Assert.NotEqual("", LogPaths.Name(root));
    }

    [Fact]
    public void Below_IsThePartUnderTheRoot()
    {
        var root = Path.Combine("music", "library");
        var file = Path.Combine(root, "Mazurka", "a.mp3");

        // What a stranger reading a bug report needs is which file and where in the library it
        // sits, both of which are still here. Where the library itself is, is not.
        Assert.Equal(Path.Combine("Mazurka", "a.mp3"), LogPaths.Below(root, file));
    }

    [Fact]
    public void Below_WhenThePathIsNotUnderTheRoot_FallsBackToTheNameAlone()
    {
        var root = Path.Combine("music", "library");

        // Neither ".." nor a rooted path may be handed back: both spell out the way from the
        // library to the file, which is the thing being kept out of the log.
        Assert.Equal("a.mp3", LogPaths.Below(root, Path.Combine("music", "elsewhere", "a.mp3")));
        Assert.Equal("library", LogPaths.Below(root, root));
        Assert.Equal("a.mp3", LogPaths.Below(null, Path.Combine("music", "a.mp3")));
    }

    [Fact]
    public void WithoutUserProfile_WritesTheProfileAsATilde()
    {
        var profile = Path.Combine("home", "someone");
        var line = $"Error loading {Path.Combine(profile, "Music", "a.mp3")}: no such file";

        var scrubbed = LogPaths.WithoutUserProfile(line, profile);

        Assert.Equal($"Error loading {Path.Combine("~", "Music", "a.mp3")}: no such file", scrubbed);
    }

    [Fact]
    public void WithoutUserProfile_TakesEveryMentionOfIt()
    {
        var profile = Path.Combine("home", "someone");
        var line = $"{Path.Combine(profile, "a.mp3")} and {Path.Combine(profile, "b.mp3")}";

        var scrubbed = LogPaths.WithoutUserProfile(line, profile);

        Assert.DoesNotContain(profile, scrubbed, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutUserProfile_WithNothingToTakeOut_LeavesTheTextAlone()
    {
        const string line = "Presentation server listening on port 8080";

        Assert.Equal(line, LogPaths.WithoutUserProfile(line, null));
        Assert.Equal(line, LogPaths.WithoutUserProfile(line, ""));
        Assert.Equal(line, LogPaths.WithoutUserProfile(line, Path.Combine("home", "someone")));
    }
}
