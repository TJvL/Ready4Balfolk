using NSubstitute;
using Ready4Balfolk.Domain.Resources;
using Ready4Balfolk.Domain.Services.Audio;
using Ready4Balfolk.Domain.Services.Logging;
using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class AudioAvailabilityTests
{
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();

    [Fact]
    public void AStartThatWasRefused_SaysTheOutputIsGoneRatherThanNothingAtAll()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger, _notifications);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Gone("Bass.ChannelPlay failed: Init");

        Assert.Equal([true, false], seen);
        Assert.Equal("Audio output is gone", Assert.Single(logger.Errors).Message);
        _notifications.Received(1).Show(DomainStrings.Audio_OutputGone, NotificationSeverity.Error);
    }

    [Fact]
    public void AnOutputThatNeverCameUp_IsToldAsWellAsLogged()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger, _notifications);

        sut.NeverCameUp(new InvalidOperationException("Bass.Init failed: Device"));

        // Nothing the DJ queues will make a sound, and a line in the log is not where they look.
        Assert.Equal(LogLevel.Critical, Assert.Single(logger.Errors).Level);
        _notifications.Received(1).Show(DomainStrings.Audio_NeverCameUp, NotificationSeverity.Error);
    }

    [Fact]
    public void AnOutputThatIsAlreadyGone_IsNotAnnouncedAgainOnEveryAttempt()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger, _notifications);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Gone("Bass.ChannelPlay failed: Init");
        sut.Gone("Bass.ChannelPlay failed: Init");
        sut.Gone("Bass.ChannelPlay failed: Init");

        Assert.Equal([true, false], seen);
        Assert.Single(logger.Errors);
        _notifications.Received(1).Show(Arg.Any<string>(), Arg.Any<NotificationSeverity>());
    }

    [Fact]
    public void SoundComingOutAgain_TakesTheWarningBackDown()
    {
        using var logger = new RecordingLoggerService();
        using var sut = new AudioAvailability(logger, _notifications);

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
        using var sut = new AudioAvailability(logger, _notifications);

        var seen = new List<bool>();
        using var subscription = sut.WhenChanged.Subscribe(seen.Add);

        sut.Working();
        sut.Working();

        Assert.Equal([true], seen);
    }
}
