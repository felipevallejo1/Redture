using Redture.Core.Color;

namespace Redture.Core.Scheduling;

/// <summary>
/// Turns a moment in time into the colour temperature the schedule wants.
/// </summary>
/// <remarks>
/// <para>
/// Pure: it takes the time as an argument rather than reading a clock, so every
/// hour of the day — and every awkward moment around midnight — is reachable in
/// a test without waiting for it.
/// </para>
/// <para>
/// The awkward part is that the cycle does not align with the calendar day.
/// Night runs across midnight, and at 01:00 the relevant sunset happened
/// yesterday. Rather than special-casing that, the evaluator lays out the
/// anchors for yesterday, today and tomorrow on one timeline and asks which
/// one was most recent. Wrapping stops being a case to handle and becomes a
/// consequence of the ordering.
/// </para>
/// </remarks>
public static class ScheduleEvaluator
{
    /// <summary>A transition anchor: the moment a change begins.</summary>
    private readonly record struct Anchor(DateTimeOffset At, bool TowardsNight, bool FromSolarTimes);

    public static ScheduleState Evaluate(DateTimeOffset now, ScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        List<Anchor> anchors = BuildAnchors(now, schedule);
        TimeSpan transition = TimeSpan.FromMinutes(schedule.TransitionMinutes);

        // The anchor in force is the last one that has already happened.
        int index = -1;
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i].At <= now)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            // Only reachable if the whole three-day window somehow lies ahead of
            // `now`; treat it as daytime rather than inventing a phase.
            return new ScheduleState(
                schedule.DayTemperatureKelvin,
                SchedulePhase.Day,
                anchors.Count > 0 ? anchors[0].At : now + TimeSpan.FromHours(1),
                UsedSolarTimes: false);
        }

        Anchor current = anchors[index];
        DateTimeOffset transitionEnd = current.At + transition;

        int from = current.TowardsNight ? schedule.DayTemperatureKelvin : schedule.NightTemperatureKelvin;
        int to = current.TowardsNight ? schedule.NightTemperatureKelvin : schedule.DayTemperatureKelvin;

        if (now < transitionEnd)
        {
            double progress = (now - current.At) / transition;

            return new ScheduleState(
                Mired.Interpolate(from, to, progress),
                current.TowardsNight ? SchedulePhase.Sunset : SchedulePhase.Sunrise,
                transitionEnd,
                current.FromSolarTimes);
        }

        DateTimeOffset nextChange = index + 1 < anchors.Count
            ? anchors[index + 1].At
            : now + TimeSpan.FromHours(12);

        return new ScheduleState(
            to,
            current.TowardsNight ? SchedulePhase.Night : SchedulePhase.Day,
            nextChange,
            current.FromSolarTimes);
    }

    /// <summary>
    /// When the next move towards daytime begins — the anchor a "pause until
    /// morning" override should expire at.
    /// </summary>
    public static DateTimeOffset NextSunrise(DateTimeOffset now, ScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        foreach (Anchor anchor in BuildAnchors(now, schedule))
        {
            if (!anchor.TowardsNight && anchor.At > now)
            {
                return anchor.At;
            }
        }

        return now + TimeSpan.FromDays(1);
    }

    /// <summary>
    /// Lays out every sunrise and sunset anchor across yesterday, today and
    /// tomorrow, in order.
    /// </summary>
    /// <remarks>
    /// Three days rather than one: at 01:00 the anchor in force is yesterday's
    /// sunset, and the next one is today's sunrise. A single day's worth would
    /// leave both ends of the night unanswerable.
    /// </remarks>
    private static List<Anchor> BuildAnchors(DateTimeOffset now, ScheduleSettings schedule)
    {
        List<Anchor> anchors = [];
        DateOnly today = DateOnly.FromDateTime(now.DateTime);

        for (int offset = -1; offset <= 1; offset++)
        {
            DateOnly date = today.AddDays(offset);

            (DateTimeOffset sunrise, DateTimeOffset sunset, bool fromSolar) = ResolveAnchors(date, now.Offset, schedule);

            anchors.Add(new Anchor(sunrise, TowardsNight: false, fromSolar));
            anchors.Add(new Anchor(sunset, TowardsNight: true, fromSolar));
        }

        anchors.Sort((left, right) => left.At.CompareTo(right.At));
        return anchors;
    }

    /// <summary>
    /// Resolves one day's anchors, preferring the sun and falling back to the
    /// configured clock times.
    /// </summary>
    /// <remarks>
    /// The fallback covers two quite different situations with the same answer:
    /// no location has been entered yet, and the sun genuinely does not cross
    /// the horizon that day. Inside the polar circles the second is a normal
    /// season, not an error.
    /// </remarks>
    private static (DateTimeOffset Sunrise, DateTimeOffset Sunset, bool FromSolar) ResolveAnchors(
        DateOnly date,
        TimeSpan localOffset,
        ScheduleSettings schedule)
    {
        if (schedule.CanUseSolarTimes
            && SolarCalculator.Calculate(date, schedule.Location!.Value) is { } solar)
        {
            return (
                solar.Sunrise.ToOffset(localOffset),
                solar.Sunset.ToOffset(localOffset),
                true);
        }

        return (
            new DateTimeOffset(date.ToDateTime(schedule.ManualSunrise), localOffset),
            new DateTimeOffset(date.ToDateTime(schedule.ManualSunset), localOffset),
            false);
    }
}
