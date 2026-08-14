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
        var settings = new ApplicationSettings() with
        {
            EndOfNightAudioPath = settingPath
        };
        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(settings);
        settingsStore.Observe().Returns(new BehaviorSubject<ApplicationSettings>(settings));

        var fileSystem = new MockFileSystem();
        foreach (var file in filesOnDisk)
        {
            fileSystem.AddFile(file, new MockFileData("not really audio"));
        }

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
}
