using Redture.Core.Color;
using Redture.Core.Scheduling;
using Xunit;

namespace Redture.Core.Tests.Scheduling;

public sealed class TemperatureSmootherTests
{
    [Fact]
    public void ASingleStepCannotCrossTheWholeRange()
    {
        // The case it exists for: automation switched on in the middle of the
        // night would otherwise snap the entire screen in one frame.
        //
        // Measured in mired, like everything else about transitions. In kelvin
        // this one frame covers over a thousand degrees, which sounds alarming
        // and is not: near the cool end of the scale a thousand kelvin is a
        // small perceptual step, which is exactly why kelvin is the wrong unit
        // to write this assertion in.
        TemperatureSmoother smoother = new(6500);
        TimeSpan frame = TimeSpan.FromMilliseconds(50);

        int afterOneFrame = smoother.Advance(3000, frame);

        double moved = Mired.FromKelvin(afterOneFrame) - Mired.FromKelvin(6500);
        double allowed = TemperatureSmoother.DefaultMiredPerSecond * frame.TotalSeconds;

        // The reported value is rounded to a whole kelvin, and one kelvin is
        // worth a fraction of a mired here, so the comparison gets exactly that
        // much slack rather than an arbitrary epsilon.
        double roundingSlack = Math.Abs(Mired.FromKelvin(afterOneFrame) - Mired.FromKelvin(afterOneFrame + 1));

        Assert.True(moved > 0d, "the smoother did not move at all");
        Assert.True(
            moved <= allowed + roundingSlack,
            $"moved {moved:0.00} mired in one frame, limit {allowed:0.00} (+{roundingSlack:0.00} rounding)");
        Assert.False(smoother.HasSettledAt(3000));
    }

    [Fact]
    public void ItArrivesExactlyRatherThanApproachingForever()
    {
        // Landing on the target matters: the caller uses "settled" to decide
        // when to stop ticking at fifty times a second.
        TemperatureSmoother smoother = new(6500);

        for (int frame = 0; frame < 200 && !smoother.HasSettledAt(3000); frame++)
        {
            smoother.Advance(3000, TimeSpan.FromMilliseconds(50));
        }

        Assert.True(smoother.HasSettledAt(3000));
        Assert.Equal(3000, smoother.CurrentKelvin);
    }

    [Fact]
    public void AFullRangeJumpTakesAboutASecondAndAHalf()
    {
        TemperatureSmoother smoother = new(6500);
        TimeSpan frame = TimeSpan.FromMilliseconds(50);
        TimeSpan elapsed = TimeSpan.Zero;

        while (!smoother.HasSettledAt(800) && elapsed < TimeSpan.FromSeconds(10))
        {
            smoother.Advance(800, frame);
            elapsed += frame;
        }

        Assert.InRange(elapsed.TotalSeconds, 1.0, 2.5);
    }

    [Fact]
    public void ScheduleSizedStepsPassStraightThrough()
    {
        // During a sunset the schedule moves a fraction of a mired per tick,
        // far below the limit, so the smoother must not slow it down at all.
        TemperatureSmoother smoother = new(6500);

        int result = smoother.Advance(6480, TimeSpan.FromSeconds(5));

        Assert.Equal(6480, result);
        Assert.True(smoother.HasSettledAt(6480));
    }

    [Fact]
    public void TheRateIsSymmetricBecauseItIsMeasuredInMired()
    {
        // Warming and cooling across the same span must take the same time. In
        // kelvin they would not: 3000 K to 6500 K is 3500 K of travel and looks
        // identical to 6500 K down to 3000 K only when measured in mired.
        TimeSpan warming = TimeToSettle(from: 6500, to: 3000);
        TimeSpan cooling = TimeToSettle(from: 3000, to: 6500);

        Assert.True(
            Math.Abs((warming - cooling).TotalMilliseconds) <= 100,
            $"warming took {warming.TotalMilliseconds} ms but cooling took {cooling.TotalMilliseconds} ms");
    }

    [Fact]
    public void SnappingSkipsTheRampEntirely()
    {
        TemperatureSmoother smoother = new(6500);

        smoother.SnapTo(2000);

        Assert.Equal(2000, smoother.CurrentKelvin);
        Assert.True(smoother.HasSettledAt(2000));
    }

    [Fact]
    public void ItNeverOvershoots()
    {
        TemperatureSmoother smoother = new(6500);
        double targetMired = Mired.FromKelvin(3000);

        for (int frame = 0; frame < 100; frame++)
        {
            int current = smoother.Advance(3000, TimeSpan.FromMilliseconds(50));
            Assert.True(
                Mired.FromKelvin(current) <= targetMired + 0.001,
                $"overshot past the target at frame {frame}: {current} K");
        }
    }

    private static TimeSpan TimeToSettle(int from, int to)
    {
        TemperatureSmoother smoother = new(from);
        TimeSpan frame = TimeSpan.FromMilliseconds(10);
        TimeSpan elapsed = TimeSpan.Zero;

        while (!smoother.HasSettledAt(to) && elapsed < TimeSpan.FromSeconds(10))
        {
            smoother.Advance(to, frame);
            elapsed += frame;
        }

        return elapsed;
    }
}
