using Ready4Balfolk.Domain.Services.Notifications;
using Ready4Balfolk.Tests.Helpers;
using Ready4Balfolk.UI.Services;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>The bars along the bottom of the window, now that nothing in the log puts one there.</summary>
public sealed class NotificationServiceTests
{
    private readonly ThrottleClock _clock = new();

    /// <summary>A failure that keeps happening is one bar, not five copies of one sentence.</summary>
    /// <remarks>
    /// The log used to be turned into bars grouped by message, which held a burst down to one.
    /// Errors now come here straight from where they happen, so this is where a burst is held down.
    /// </remarks>
    [Fact]
    public void AnErrorAlreadyOnScreen_IsNotPutUpAgainBesideItself()
    {
        using var sut = new NotificationService(_clock.Scheduler);
        sut.WindowOpened();

        sut.Show("De wijzigingen in de muziekmap konden niet verwerkt worden", NotificationSeverity.Error);
        sut.Show("De wijzigingen in de muziekmap konden niet verwerkt worden", NotificationSeverity.Error);
        sut.Show("De wijzigingen in de muziekmap konden niet verwerkt worden", NotificationSeverity.Error);

        Assert.Single(sut.Notifications);

        // Gone from the screen, it can be said again: it is happening again.
        _clock.MoveOn(TimeSpan.FromSeconds(5));
        sut.Show("De wijzigingen in de muziekmap konden niet verwerkt worden", NotificationSeverity.Error);

        Assert.Single(sut.Notifications);
    }

    /// <summary>What was said while the application was being put together waits for the window.</summary>
    /// <remarks>
    /// A settings file that would not read is found before there is a window, and its four seconds
    /// used to run out before anybody could read it. The log used to keep the last few errors and
    /// replay them once the window opened, which is what this replaces.
    /// </remarks>
    [Fact]
    public void WhatIsSaidBeforeTheWindowOpens_IsStillThereWhenItDoes()
    {
        using var sut = new NotificationService(_clock.Scheduler);

        sut.Show("Het instellingenbestand kon niet gelezen worden", NotificationSeverity.Error);
        _clock.MoveOn(TimeSpan.FromSeconds(30));

        Assert.Single(sut.Notifications);

        sut.WindowOpened();
        _clock.MoveOn(TimeSpan.FromSeconds(5));

        Assert.Empty(sut.Notifications);
    }
}
