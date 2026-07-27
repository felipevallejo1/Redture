using Redture.Core.Brightness;
using Redture.Core.Settings;
using Xunit;

namespace Redture.Core.Tests.Brightness;

public sealed class BrightnessMapperTests
{
    private const double MaxOpacity = 0.92d;

    // --- Software-only path (no DDC/CI, risk R12) ---------------------------

    [Fact]
    public void WithoutHardware_FullBrightnessAppliesNoOverlay()
    {
        BrightnessPlan plan = BrightnessMapper.Map(100, MaxOpacity, hardwareAvailable: false);

        Assert.Null(plan.HardwareBrightness);
        Assert.Equal(0d, plan.OverlayOpacity);
        Assert.True(plan.IsOverlayIdle);
    }

    [Fact]
    public void WithoutHardware_ZeroBrightnessAppliesTheConfiguredMaximum()
    {
        BrightnessPlan plan = BrightnessMapper.Map(0, MaxOpacity, hardwareAvailable: false);

        Assert.Equal(MaxOpacity, plan.OverlayOpacity);
    }

    [Fact]
    public void WithoutHardware_TheWholeRangeIsDrivenByTheOverlay()
    {
        BrightnessPlan plan = BrightnessMapper.Map(50, MaxOpacity, hardwareAvailable: false);

        Assert.Equal(MaxOpacity * 0.5d, plan.OverlayOpacity, precision: 10);
    }

    // --- Two-segment path ---------------------------------------------------

    [Theory]
    [InlineData(100, 100)]
    [InlineData(65, 50)]
    [InlineData(30, 0)]
    public void WithHardware_UpperSegmentDrivesTheBacklightOnly(double brightness, double expectedHardware)
    {
        BrightnessPlan plan = BrightnessMapper.Map(brightness, MaxOpacity, hardwareAvailable: true);

        Assert.Equal(expectedHardware, plan.HardwareBrightness!.Value, precision: 10);
        Assert.Equal(0d, plan.OverlayOpacity);
    }

    [Theory]
    [InlineData(15, 0.5)]
    [InlineData(0, 1.0)]
    public void WithHardware_LowerSegmentDrivesTheOverlayOnly(double brightness, double expectedFraction)
    {
        BrightnessPlan plan = BrightnessMapper.Map(brightness, MaxOpacity, hardwareAvailable: true);

        Assert.Equal(0d, plan.HardwareBrightness!.Value);
        Assert.Equal(MaxOpacity * expectedFraction, plan.OverlayOpacity, precision: 10);
    }

    [Fact]
    public void WithHardware_TheHandoverIsContinuous()
    {
        // Either side of the split point the perceived step must be tiny: this
        // is what makes a single slider feel like one control instead of two.
        BrightnessPlan justAbove = BrightnessMapper.Map(30.001, MaxOpacity, hardwareAvailable: true);
        BrightnessPlan justBelow = BrightnessMapper.Map(29.999, MaxOpacity, hardwareAvailable: true);

        Assert.Equal(0d, justAbove.OverlayOpacity);
        Assert.True(justBelow.OverlayOpacity < 0.001d, $"opacity jumped to {justBelow.OverlayOpacity}");
        Assert.True(justAbove.HardwareBrightness!.Value < 0.01d);
    }

    // --- Safety -------------------------------------------------------------

    [Fact]
    public void OpacityIsNeverAllowedToReachFullyOpaque()
    {
        // Even if a hand-edited settings file asks for it.
        BrightnessPlan plan = BrightnessMapper.Map(0, maxOverlayOpacity: 5d, hardwareAvailable: false);

        Assert.Equal(AppSettings.AbsoluteMaxOverlayOpacity, plan.OverlayOpacity);
        Assert.True(plan.OverlayOpacity < 1d);
    }

    [Theory]
    [InlineData(-40)]
    [InlineData(400)]
    public void OutOfRangeBrightnessIsClamped(double brightness)
    {
        BrightnessPlan plan = BrightnessMapper.Map(brightness, MaxOpacity, hardwareAvailable: false);

        Assert.InRange(plan.OverlayOpacity, 0d, MaxOpacity);
    }

    [Fact]
    public void OverlayOpacityDecreasesMonotonicallyWithBrightness()
    {
        double previous = double.MaxValue;

        for (double brightness = 0; brightness <= 100; brightness += 0.5)
        {
            double opacity = BrightnessMapper.Map(brightness, MaxOpacity, hardwareAvailable: true).OverlayOpacity;
            Assert.True(opacity <= previous, $"opacity rose at brightness {brightness}");
            previous = opacity;
        }
    }
}
