using Redture.Core.Scheduling;
using Xunit;

namespace Redture.Core.Tests.Scheduling;

public sealed class ScheduleOverrideTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 15, 22, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void ATimedOverrideLapsesOnItsOwn()
    {
        ScheduleOverride paused = ScheduleOverride.For(Now, TimeSpan.FromHours(1), "Paused for an hour");

        Assert.True(paused.IsActiveAt(Now));
        Assert.True(paused.IsActiveAt(Now.AddMinutes(59)));
        Assert.False(paused.IsActiveAt(Now.AddMinutes(61)));
    }

    [Fact]
    public void CinemaModeRunsUntilItIsCancelled()
    {
        ScheduleOverride cinema = ScheduleOverride.Indefinite("Cinema mode");

        Assert.True(cinema.IsActiveAt(Now));
        Assert.True(cinema.IsActiveAt(Now.AddDays(3)));
        Assert.Null(cinema.RemainingAt(Now));
    }

    [Fact]
    public void RemainingTimeCountsDown()
    {
        ScheduleOverride paused = ScheduleOverride.For(Now, TimeSpan.FromHours(1), "Paused");

        Assert.Equal(TimeSpan.FromMinutes(60), paused.RemainingAt(Now));
        Assert.Equal(TimeSpan.FromMinutes(15), paused.RemainingAt(Now.AddMinutes(45)));
    }

    [Fact]
    public void RemainingTimeNeverGoesNegative()
    {
        // The UI formats this directly, and "resuming in -3 min" is not a thing
        // anyone should read.
        ScheduleOverride paused = ScheduleOverride.For(Now, TimeSpan.FromHours(1), "Paused");

        Assert.Equal(TimeSpan.Zero, paused.RemainingAt(Now.AddHours(5)));
    }

    [Fact]
    public void PausingUntilSunriseLandsOnTheNextMorning()
    {
        ScheduleSettings schedule = new()
        {
            UseSolarTimes = false,
            ManualSunrise = new TimeOnly(7, 0),
            ManualSunset = new TimeOnly(20, 0),
        };

        // Late evening: the next move towards daytime is tomorrow morning, not
        // this morning, which already happened.
        DateTimeOffset nextSunrise = ScheduleEvaluator.NextSunrise(Now, schedule);

        Assert.Equal(new DateTimeOffset(2026, 5, 16, 7, 0, 0, TimeSpan.FromHours(-3)), nextSunrise);
    }

    [Fact]
    public void PausingUntilSunriseFromTheSmallHoursLandsTheSameDay()
    {
        ScheduleSettings schedule = new()
        {
            UseSolarTimes = false,
            ManualSunrise = new TimeOnly(7, 0),
            ManualSunset = new TimeOnly(20, 0),
        };

        DateTimeOffset threeInTheMorning = new(2026, 5, 15, 3, 0, 0, TimeSpan.FromHours(-3));
        DateTimeOffset nextSunrise = ScheduleEvaluator.NextSunrise(threeInTheMorning, schedule);

        Assert.Equal(new DateTimeOffset(2026, 5, 15, 7, 0, 0, TimeSpan.FromHours(-3)), nextSunrise);
    }
}
