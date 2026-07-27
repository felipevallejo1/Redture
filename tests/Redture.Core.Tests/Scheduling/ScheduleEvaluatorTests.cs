using Redture.Core.Color;
using Redture.Core.Scheduling;
using Xunit;

namespace Redture.Core.Tests.Scheduling;

public sealed class ScheduleEvaluatorTests
{
    private static readonly TimeSpan Local = TimeSpan.FromHours(-3);

    /// <summary>Fixed clock times, so every assertion is about the schedule
    /// rather than about the sun.</summary>
    private static ScheduleSettings Manual() => new()
    {
        UseSolarTimes = false,
        ManualSunrise = new TimeOnly(7, 0),
        ManualSunset = new TimeOnly(20, 0),
        DayTemperatureKelvin = 6500,
        NightTemperatureKelvin = 3000,
        TransitionMinutes = 60,
    };

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 5, 15, hour, minute, 0, Local);

    [Fact]
    public void MiddayHoldsTheDaytimeTemperature()
    {
        ScheduleState state = ScheduleEvaluator.Evaluate(At(13), Manual());

        Assert.Equal(SchedulePhase.Day, state.Phase);
        Assert.Equal(6500, state.TemperatureKelvin);
    }

    [Fact]
    public void TheSmallHoursHoldTheNightTemperature()
    {
        // The anchor in force here is *yesterday's* sunset. Getting this wrong
        // is the classic scheduling bug: the day rolls over at midnight but the
        // night does not.
        ScheduleState state = ScheduleEvaluator.Evaluate(At(3), Manual());

        Assert.Equal(SchedulePhase.Night, state.Phase);
        Assert.Equal(3000, state.TemperatureKelvin);
    }

    [Fact]
    public void JustBeforeMidnightIsAlsoNight()
    {
        ScheduleState state = ScheduleEvaluator.Evaluate(At(23, 59), Manual());

        Assert.Equal(SchedulePhase.Night, state.Phase);
        Assert.Equal(3000, state.TemperatureKelvin);
    }

    [Fact]
    public void NothingChangesAcrossMidnight()
    {
        // A minute either side of midnight must look identical. If the anchors
        // were rebuilt per calendar day, this is where it would show.
        ScheduleState before = ScheduleEvaluator.Evaluate(At(23, 59), Manual());
        ScheduleState after = ScheduleEvaluator.Evaluate(
            new DateTimeOffset(2026, 5, 16, 0, 1, 0, Local),
            Manual());

        Assert.Equal(before.Phase, after.Phase);
        Assert.Equal(before.TemperatureKelvin, after.TemperatureKelvin);
    }

    [Fact]
    public void TheSunsetTransitionStartsAtTheAnchorAndEndsAfterTheDuration()
    {
        ScheduleSettings schedule = Manual();

        ScheduleState atAnchor = ScheduleEvaluator.Evaluate(At(20, 0), schedule);
        ScheduleState midway = ScheduleEvaluator.Evaluate(At(20, 30), schedule);
        ScheduleState atEnd = ScheduleEvaluator.Evaluate(At(21, 0), schedule);

        Assert.Equal(SchedulePhase.Sunset, atAnchor.Phase);
        Assert.Equal(6500, atAnchor.TemperatureKelvin);

        Assert.Equal(SchedulePhase.Sunset, midway.Phase);
        Assert.InRange(midway.TemperatureKelvin, 3001, 6499);

        Assert.Equal(SchedulePhase.Night, atEnd.Phase);
        Assert.Equal(3000, atEnd.TemperatureKelvin);
    }

    [Fact]
    public void TheHalfwayPointIsHalfwayInMiredNotInKelvin()
    {
        // The whole reason transitions are interpolated in mired. Halfway
        // between 6500 K and 3000 K is 4100 K, not the 4750 K a linear kelvin
        // interpolation would give -- and 4100 K is what looks halfway.
        ScheduleState midway = ScheduleEvaluator.Evaluate(At(20, 30), Manual());

        Assert.InRange(midway.TemperatureKelvin, 4080, 4120);
    }

    [Fact]
    public void TheSunriseTransitionRunsTheOtherWay()
    {
        ScheduleSettings schedule = Manual();

        ScheduleState atAnchor = ScheduleEvaluator.Evaluate(At(7, 0), schedule);
        ScheduleState atEnd = ScheduleEvaluator.Evaluate(At(8, 0), schedule);

        Assert.Equal(SchedulePhase.Sunrise, atAnchor.Phase);
        Assert.Equal(3000, atAnchor.TemperatureKelvin);

        Assert.Equal(SchedulePhase.Day, atEnd.Phase);
        Assert.Equal(6500, atEnd.TemperatureKelvin);
    }

    [Fact]
    public void TheTemperatureNeverJumpsPerceptiblyAcrossAWholeDay()
    {
        // Sampled every minute for 48 hours, across both anchors and both
        // midnights: no single minute may move the temperature more than a
        // sixtieth of the whole transition.
        //
        // Measured in mired, not kelvin. In kelvin the steps are deliberately
        // uneven -- the last minute of a sunrise covers 124 K while the first
        // covers 8 K -- and asserting evenness there would be asserting the
        // opposite of what the design is for. Mired is where the steps are
        // equal, and where equal means equal to the eye.
        ScheduleSettings schedule = Manual();
        DateTimeOffset start = new(2026, 5, 15, 0, 0, 0, Local);

        double totalMired = Math.Abs(
            Mired.FromKelvin(schedule.NightTemperatureKelvin) - Mired.FromKelvin(schedule.DayTemperatureKelvin));

        // One minute's worth of the transition, with a little room for rounding
        // to the nearest whole kelvin.
        double maxStep = (totalMired / schedule.TransitionMinutes) + 0.5;

        double previous = Mired.FromKelvin(ScheduleEvaluator.Evaluate(start, schedule).TemperatureKelvin);

        for (int minute = 1; minute <= 48 * 60; minute++)
        {
            double current = Mired.FromKelvin(
                ScheduleEvaluator.Evaluate(start.AddMinutes(minute), schedule).TemperatureKelvin);

            Assert.True(
                Math.Abs(current - previous) <= maxStep,
                $"temperature moved {Math.Abs(current - previous):0.00} mired in one minute at minute {minute}, limit {maxStep:0.00}");

            previous = current;
        }
    }

    [Fact]
    public void NextChangePointsAtTheEndOfATransitionWhileOneIsRunning()
    {
        ScheduleState state = ScheduleEvaluator.Evaluate(At(20, 15), Manual());

        Assert.Equal(At(21, 0), state.NextChangeAt);
    }

    [Fact]
    public void NextChangePointsAtTheFollowingAnchorWhenSteady()
    {
        ScheduleState state = ScheduleEvaluator.Evaluate(At(13), Manual());

        Assert.Equal(At(20, 0), state.NextChangeAt);
    }

    [Fact]
    public void SolarTimesAreUsedWhenALocationIsSet()
    {
        ScheduleSettings schedule = Manual();
        schedule.UseSolarTimes = true;
        schedule.Latitude = -34.6037;
        schedule.Longitude = -58.3816;

        ScheduleState state = ScheduleEvaluator.Evaluate(At(13), schedule);

        Assert.True(state.UsedSolarTimes);
    }

    [Fact]
    public void SolarTimesFallBackToTheClockWhenNoLocationIsSet()
    {
        // Asking to follow the sun without saying where you are cannot work.
        // Falling back silently would be worse than falling back visibly, hence
        // the flag on the result.
        ScheduleSettings schedule = Manual();
        schedule.UseSolarTimes = true;
        schedule.Latitude = null;
        schedule.Longitude = null;

        ScheduleState state = ScheduleEvaluator.Evaluate(At(13), schedule);

        Assert.False(state.UsedSolarTimes);
        Assert.Equal(SchedulePhase.Day, state.Phase);
    }

    [Fact]
    public void PolarSummerFallsBackToTheClockInsteadOfBreaking()
    {
        // Above the Arctic circle in June there is no sunset to anchor to. The
        // schedule still has to produce an answer every minute of the day.
        ScheduleSettings schedule = Manual();
        schedule.UseSolarTimes = true;
        schedule.Latitude = 78.2232;
        schedule.Longitude = 15.6469;

        ScheduleState state = ScheduleEvaluator.Evaluate(
            new DateTimeOffset(2026, 6, 21, 23, 0, 0, TimeSpan.FromHours(2)),
            schedule);

        Assert.False(state.UsedSolarTimes);
        Assert.Equal(SchedulePhase.Night, state.Phase);
    }
}
