using Microsoft.Extensions.Time.Testing;
using Ready4Balfolk.Web.Security;

namespace Ready4Balfolk.Tests.Unit;

/// <summary>
/// The remote's only guard. The hub checks the token rather than the form, so a hole here is a hole
/// in the queue for anyone who can reach the port.
/// </summary>
public sealed class RemoteAccessServiceTests
{
    private const string Pin = "123456";
    private const string Client = "192.168.1.50";

    private static RemoteAccessService Enabled(FakeTimeProvider? time = null)
    {
        var sut = new RemoteAccessService(time ?? new FakeTimeProvider());
        sut.Configure(true, Pin);
        return sut;
    }

    // --- TryLogin ---

    [Fact]
    public void TryLogin_CorrectPin_GrantsAUsableToken()
    {
        var sut = Enabled();

        var result = sut.TryLogin(Pin, Client);

        Assert.True(result.IsGranted);
        Assert.NotNull(result.Token);
        Assert.True(sut.IsTokenValid(result.Token));
    }

    [Fact]
    public void TryLogin_WrongPin_IsRejectedAndIssuesNothing()
    {
        var sut = Enabled();

        var result = sut.TryLogin("000000", Client);

        Assert.False(result.IsGranted);
        Assert.Equal("rejected", result.Status);
        Assert.Null(result.Token);
    }

    [Fact]
    public void TryLogin_WhenDisabled_RefusesEvenTheRightPin()
    {
        var sut = new RemoteAccessService(new FakeTimeProvider());
        sut.Configure(false, Pin);

        var result = sut.TryLogin(Pin, Client);

        Assert.False(result.IsGranted);
        Assert.Equal("disabled", result.Status);
    }

    [Fact]
    public void TryLogin_NoPinSet_RefusesAnEmptyAttempt()
    {
        // Otherwise "no PIN configured" would mean "any PIN works", which is the worst default.
        var sut = new RemoteAccessService(new FakeTimeProvider());
        sut.Configure(true, "");

        Assert.False(sut.TryLogin("", Client).IsGranted);
        Assert.False(sut.TryLogin(null, Client).IsGranted);
    }

    [Fact]
    public void TryLogin_FiveWrongAttempts_LocksTheAddressOut()
    {
        var sut = Enabled();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal("rejected", sut.TryLogin("000000", Client).Status);
        }

        var locked = sut.TryLogin("000000", Client);

        Assert.Equal("locked", locked.Status);
        Assert.True(locked.RetryAfterSeconds > 0);
        // A six digit PIN would fall in seconds to an unthrottled attacker.
        Assert.Equal("locked", sut.TryLogin(Pin, Client).Status);
    }

    [Fact]
    public void TryLogin_AfterTheLockoutExpires_TheRightPinWorksAgain()
    {
        var time = new FakeTimeProvider();
        var sut = Enabled(time);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            sut.TryLogin("000000", Client);
        }

        time.Advance(TimeSpan.FromMinutes(2));

        Assert.True(sut.TryLogin(Pin, Client).IsGranted);
    }

    [Fact]
    public void TryLogin_LockoutIsPerAddress()
    {
        var sut = Enabled();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            sut.TryLogin("000000", Client);
        }

        Assert.True(sut.TryLogin(Pin, "192.168.1.51").IsGranted);
    }

    // --- IsTokenValid ---

    [Fact]
    public void IsTokenValid_UnknownToken_IsRefused()
    {
        var sut = Enabled();

        Assert.False(sut.IsTokenValid("not-a-token"));
        Assert.False(sut.IsTokenValid(null));
    }

    [Fact]
    public void IsTokenValid_AfterTheTokenLifetime_IsRefused()
    {
        // The value stored against a token used to be its issue time and nothing read it, so a
        // token stayed usable until somebody happened to change the PIN.
        var time = new FakeTimeProvider();
        var sut = Enabled(time);
        var token = sut.TryLogin(Pin, Client).Token;

        time.Advance(TimeSpan.FromHours(13));

        Assert.False(sut.IsTokenValid(token));
    }

    [Fact]
    public void IsTokenValid_UsedThroughTheEvening_SlidesRatherThanExpiring()
    {
        // A phone reconnects every time it sleeps. Being asked for the PIN again in front of a room
        // is the thing the token exists to avoid, so use has to push the expiry out.
        var time = new FakeTimeProvider();
        var sut = Enabled(time);
        var token = sut.TryLogin(Pin, Client).Token;

        for (var hour = 0; hour < 10; hour++)
        {
            time.Advance(TimeSpan.FromHours(8));
            Assert.True(sut.IsTokenValid(token));
        }
    }

    [Fact]
    public void TryLogin_OverManyLogins_DoesNotKeepEveryTokenForever()
    {
        // One entry per login, never removed, on a process that runs all evening.
        var time = new FakeTimeProvider();
        var sut = Enabled(time);

        var first = sut.TryLogin(Pin, Client).Token;
        for (var login = 0; login < 50; login++)
        {
            sut.TryLogin(Pin, Client);
        }

        time.Advance(TimeSpan.FromHours(13));
        var latest = sut.TryLogin(Pin, Client).Token;

        Assert.False(sut.IsTokenValid(first));
        Assert.True(sut.IsTokenValid(latest));
    }

    /// <remarks>
    /// A guard, not a regression test. Pruning an address that is holding neither failures nor a
    /// live lockout is not observable from out here, so what this pins down is the behaviour that
    /// has to survive it: the pruned address is back to a full five attempts, not locked and not
    /// one guess from it.
    /// </remarks>
    [Fact]
    public void TryLogin_AfterALockoutHasPassed_TheAddressGetsItsAttemptsBack()
    {
        var time = new FakeTimeProvider();
        var sut = Enabled(time);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            sut.TryLogin("000000", Client);
        }

        time.Advance(TimeSpan.FromMinutes(2));
        // A successful login from any address is what triggers a prune.
        sut.TryLogin(Pin, "192.168.1.99");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal("rejected", sut.TryLogin("000000", Client).Status);
        }
    }

    [Fact]
    public void Configure_ChangingThePin_DropsEveryIssuedToken()
    {
        var sut = Enabled();
        var token = sut.TryLogin(Pin, Client).Token;

        sut.Configure(true, "654321");

        Assert.False(sut.IsTokenValid(token));
    }

    [Fact]
    public void Configure_SwitchingTheRemoteOff_DropsEveryIssuedToken()
    {
        var sut = Enabled();
        var token = sut.TryLogin(Pin, Client).Token;

        sut.Configure(false, Pin);

        Assert.False(sut.IsTokenValid(token));
    }

    [Fact]
    public void Configure_SamePinAgain_KeepsConnectedPhonesConnected()
    {
        var sut = Enabled();
        var token = sut.TryLogin(Pin, Client).Token;

        // Any unrelated settings change re-applies the options, and that must not kick everyone off.
        sut.Configure(true, Pin);

        Assert.True(sut.IsTokenValid(token));
    }

    // --- GeneratePin ---

    [Fact]
    public void GeneratePin_IsSixDigits()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var pin = RemoteAccessService.GeneratePin();

            Assert.Equal(6, pin.Length);
            Assert.All(pin, character => Assert.True(char.IsAsciiDigit(character)));
        }
    }
}
