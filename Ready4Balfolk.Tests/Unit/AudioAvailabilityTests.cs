using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class AudioAvailabilityTests
{
    [Fact]
    public void AStartThatWasRefused_SaysTheOutputIsGoneRatherThanNothingAtAll()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Gone("Bass.ChannelPlay failed: Init");

        Assert.Equal([true, false], seen);
        Assert.Equal(DomainStrings.Audio_OutputGone, Assert.Single(logger.Errors).Message);
    }

    [Fact]
    public void AnOutputThatIsAlreadyGone_IsNotAnnouncedAgainOnEveryAttempt()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Gone("Bass.ChannelPlay failed: Init");
        sut.Gone("Bass.ChannelPlay failed: Init");
        sut.Gone("Bass.ChannelPlay failed: Init");

        Assert.Equal([true, false], seen);
        Assert.Single(logger.Errors);
    }

    [Fact]
    public void SoundComingOutAgain_TakesTheWarningBackDown()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Gone("Bass.ChannelPlay failed: Init");
        sut.Working();

        Assert.Equal([true, false, true], seen);
    }

    [Fact]
    public void AStartThatWorked_ChangesNothingWhileTheOutputWasThereAllAlong()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Working();
        sut.Working();

        Assert.Equal([true], seen);
    }
}
