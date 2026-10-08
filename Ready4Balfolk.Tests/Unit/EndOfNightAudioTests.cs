using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Subjects;
using NSubstitute;
using Ready4Balfolk.Domain.Models.Settings;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Queue;
using Ready4Balfolk.Domain.Stores.Settings;

namespace Ready4Balfolk.Tests.Unit;

public sealed class EndOfNightAudioTests
{
    // Absolute in this platform's own notation, since that is what a picker hands back and what
    // System.Uri accepts on Windows.
    private static readonly string ChosenPath = Path.GetFullPath("/audio/last-waltz.mp3");

    private static EndOfNightAudio CreateSut(string settingPath, params string[] filesOnDisk)
    {
        var fileSystem = new MockFileSystem();
        foreach (var file in filesOnDisk)
        {
            fileSystem.AddFile(file, new MockFileData("not really audio"));
        }

        return CreateSut(settingPath, fileSystem);
    }

    private static EndOfNightAudio CreateSut(string settingPath, MockFileSystem fileSystem)
    {
        var settings = new ApplicationSettings() with
        {
            EndOfNightAudioPath = settingPath
        };
        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        return new EndOfNightAudio(settingsStore, fileSystem, new NoOpLoggerService());
    }

    [Fact]
    public void NothingChosen_IsNotAvailable() =>
        Assert.False(CreateSut("").IsAvailable);

    [Fact]
    public void NothingChosen_CreatesNothing() =>
        Assert.Null(CreateSut("").Create());

    [Fact]
    public void ChosenFileGone_IsNotAvailable() =>
        Assert.False(CreateSut(ChosenPath).IsAvailable);

    [Fact]
    public void ChosenFilePresent_IsAvailable() =>
        Assert.True(CreateSut(ChosenPath, ChosenPath).IsAvailable);

    [Fact]
    public void Create_CarriesTheResolvedPath()
    {
        var item = CreateSut(ChosenPath, ChosenPath).Create();

        Assert.NotNull(item);
        Assert.Equal(ChosenPath, item.FilePath);
    }

    [Fact]
    public void Create_UnreadableFile_StillPlaysWithoutALength()
    {
        // A file that will not say how long it is contributes nothing to the projection, which
        // beats refusing to end the evening over a missing header.
        var item = CreateSut(ChosenPath, ChosenPath).Create();

        Assert.NotNull(item);
        Assert.Null(item.Duration);
    }

    [Fact]
    public void Create_ReadsTheLengthFromTheFileSystemItWasGiven()
    {
        // The existence check and the read have to agree on which disk the file is on. Reading the
        // length from the real disk would find nothing at this path and quietly leave the
        // projection short by the last dance of the night.
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile(ChosenPath, new MockFileData(EmbeddedAudio("scale.mp3")));

        var item = CreateSut(ChosenPath, fileSystem).Create();

        Assert.NotNull(item);
        Assert.NotNull(item.Duration);
        Assert.True(item.Duration > TimeSpan.Zero);
    }

    private static byte[] EmbeddedAudio(string name)
    {
        using var resource = typeof(EndOfNightAudioTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded audio '{name}' is missing.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }
}
