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

    /// <summary>Puts the evening at this time of day, and says what the clock now reads.</summary>
    /// <remarks>
    /// For the scenarios whose subject is a time of day rather than a length of time. Read off the
    /// wall clock instead, one of those is a different scenario at every hour: a cutoff taken from
    /// nine in the evening and one taken from ten to midnight are not the same question, because
    /// the second of them crosses into the next day while the run is still going. The clock only
    /// ever moves forward, so an hour that has gone by today is the same hour tomorrow.
    /// </remarks>
    public DateTime MoveOnToTimeOfDay(TimeOnly timeOfDay)
    {
        var today = GetLocalNow().Date;
        var target = TheInstantOf(today, timeOfDay);

        if (target <= GetUtcNow())
        {
            target = TheInstantOf(today.AddDays(1), timeOfDay);
        }

        MoveOn(target - GetUtcNow());
        return target.DateTime;
    }

    /// <summary>The one moment in time at which the wall clock here reads that day at that hour.</summary>
    /// <remarks>
    /// Through the local zone rather than by subtracting two wall-clock readings, because on the
    /// two nights a year the offset changes those two are not the same length: the span between
    /// tonight at ten and tomorrow at nine is twenty-three hours on one of them and twenty-five on
    /// the other, and a clock moved on by the wrong one of those lands an hour off the hour it was
    /// asked for while the cutoff read back off it still says nine.
    /// </remarks>
    private DateTimeOffset TheInstantOf(DateTime day, TimeOnly timeOfDay)
    {
        var wallClock = day.Date + timeOfDay.ToTimeSpan();

        return new DateTimeOffset(wallClock, LocalTimeZone.GetUtcOffset(wallClock));
    }
}
