namespace Ready4Balfolk.E2E;

/// <summary>The clock the application reads, which a scenario can move on.</summary>
/// <remarks>
/// <para>
/// Real time, plus however far the scenario has pushed it. Not a frozen clock: a scenario still
/// plays real audio and waits for real countdowns, so time has to keep passing at its own speed.
/// What it must also be able to do is jump, because a cutoff grace is minutes away, an unfinished
/// night is eight hours old and a remote's token lasts half a day, and no suite can sit through
/// any of that.
/// </para>
/// <para>
/// Timers are left alone deliberately. Moving the clock on does not fire a timer early, so what a
/// jump changes is only what the application concludes when it next asks what time it is, which is
/// exactly the thing these scenarios are about.
/// </para>
/// </remarks>
public sealed class ScenarioClock : TimeProvider
{
    private TimeSpan _pushedOn = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + _pushedOn;

    public override long GetTimestamp() => System.GetTimestamp();

    /// <summary>Puts the evening this much further along.</summary>
    public void MoveOn(TimeSpan howFar) => _pushedOn += howFar;
}
