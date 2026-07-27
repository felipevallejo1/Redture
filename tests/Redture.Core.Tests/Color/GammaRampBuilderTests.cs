using Redture.Core.Color;
using Redture.Core.Settings;
using Xunit;

namespace Redture.Core.Tests.Color;

public sealed class GammaRampBuilderTests
{
    [Fact]
    public void NeutralTemperatureProducesExactlyTheIdentityRamp()
    {
        // The whole point of expressing gains relative to the neutral white
        // point: "no tint" must mean the display is genuinely untouched, not
        // tinted by the small offset between the Planckian locus at 6500 K and
        // sRGB's D65 white.
        GammaRamp ramp = GammaRampBuilder.Build(AppSettings.NeutralTemperatureKelvin);

        Assert.True(ramp.HasSameValues(GammaRamp.Linear));
    }

    [Fact]
    public void WarmTemperaturesAttenuateBlueMostAndRedNotAtAll()
    {
        (double r, double g, double b) = PlanckianLocus.EncodedGainsFor(2700);

        Assert.Equal(1d, r, precision: 10);   // Red is the peak, so it is never reduced.
        Assert.True(g < r, $"green gain {g} should be below red {r}");
        Assert.True(b < g, $"blue gain {b} should be below green {g}");
    }

    [Fact]
    public void CoolTemperaturesAttenuateRedInstead()
    {
        (double r, double g, double b) = PlanckianLocus.EncodedGainsFor(9000);

        Assert.True(r < 1d, $"red gain {r} should be reduced above the neutral point");
        Assert.True(r < b, $"red gain {r} should be below blue {b}");
    }

    [Fact]
    public void NoChannelIsEverBoosted()
    {
        // A gamma ramp cannot make a display emit more light than it already
        // does; a gain above 1 would only clip highlights and lose detail.
        for (int kelvin = AppSettings.MinTemperatureKelvin; kelvin <= AppSettings.MaxTemperatureKelvin; kelvin += 25)
        {
            (double r, double g, double b) = PlanckianLocus.EncodedGainsFor(kelvin);

            Assert.InRange(r, 0d, 1d);
            Assert.InRange(g, 0d, 1d);
            Assert.InRange(b, 0d, 1d);
        }
    }

    [Fact]
    public void BlueAttenuationIncreasesAsTheTemperatureDrops()
    {
        // The property that makes the slider feel right: every step towards
        // warm must remove more blue than the step before, with no reversals.
        double previousBlue = double.MinValue;

        for (int kelvin = AppSettings.MinTemperatureKelvin; kelvin <= AppSettings.NeutralTemperatureKelvin; kelvin += 25)
        {
            (double _, double _, double blue) = PlanckianLocus.EncodedGainsFor(kelvin);

            Assert.True(blue >= previousBlue, $"blue gain fell at {kelvin} K: {blue} after {previousBlue}");
            previousBlue = blue;
        }
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(4000)]
    [InlineData(6500)]
    [InlineData(10000)]
    public void EveryChannelIsMonotonic(int kelvin)
    {
        // A non-monotonic ramp inverts tones somewhere in the range, which
        // shows up as posterised banding.
        GammaRamp ramp = GammaRampBuilder.Build(kelvin);

        for (int channel = 0; channel < GammaRamp.Channels; channel++)
        {
            for (int level = 1; level < ramp.LevelsPerChannel; level++)
            {
                Assert.True(
                    ramp[channel, level] >= ramp[channel, level - 1],
                    $"channel {channel} fell between levels {level - 1} and {level}");
            }
        }
    }

    [Fact]
    public void BlackStaysBlackAtEveryTemperature()
    {
        // Entry zero must map to zero, otherwise the display's black level
        // rises and contrast is lost -- the exact failure a tinted overlay
        // would have caused, which is why the tint lives here instead.
        for (int kelvin = AppSettings.MinTemperatureKelvin; kelvin <= AppSettings.MaxTemperatureKelvin; kelvin += 100)
        {
            GammaRamp ramp = GammaRampBuilder.Build(kelvin);

            for (int channel = 0; channel < GammaRamp.Channels; channel++)
            {
                Assert.Equal(0, ramp[channel, 0]);
            }
        }
    }

    [Fact]
    public void TheIdentityRampMapsWhiteToFullScale()
    {
        for (int channel = 0; channel < GammaRamp.Channels; channel++)
        {
            Assert.Equal(GammaRamp.MaxValue, GammaRamp.Linear[channel, GammaRamp.Linear.LevelsPerChannel - 1]);
        }
    }

    [Theory]
    [InlineData(-500)]
    [InlineData(50_000)]
    public void OutOfRangeTemperaturesAreClamped(int kelvin)
    {
        GammaRamp ramp = GammaRampBuilder.Build(kelvin);

        int expected = Math.Clamp(kelvin, AppSettings.MinTemperatureKelvin, AppSettings.MaxTemperatureKelvin);
        Assert.True(ramp.HasSameValues(GammaRampBuilder.Build(expected)));
    }

    [Fact]
    public void RampsCanBeBuiltAtWhateverSizeTheDisplayAsksFor()
    {
        // XRandR reports a ramp size per CRTC, commonly 1024 or 2048. Writing a
        // 256-entry table into one of those would fill an eighth of it and
        // leave the rest holding whatever was there before.
        GammaRamp large = GammaRampBuilder.Build(3000, levelsPerChannel: 1024);

        Assert.Equal(1024, large.LevelsPerChannel);
        Assert.Equal(GammaRamp.Channels * 1024, large.Values.Length);
        Assert.Equal(0, large[0, 0]);
        Assert.Equal(GammaRamp.MaxValue, large[0, 1023]);

        // Same curve, different resolution: the endpoints must still agree with
        // the 256-entry version.
        GammaRamp small = GammaRampBuilder.Build(3000);
        Assert.Equal(small[2, 255], large[2, 1023]);
    }

    [Fact]
    public void RampsOfDifferentSizesAreNeverConsideredEqual()
    {
        GammaRamp small = GammaRampBuilder.Build(3000);
        GammaRamp large = GammaRampBuilder.Build(3000, levelsPerChannel: 1024);

        Assert.False(small.HasSameValues(large));
    }

    [Fact]
    public void RampsBuiltFromTheSameTemperatureCompareEqual()
    {
        // What lets the controller skip a redundant write to the driver.
        Assert.True(GammaRampBuilder.Build(3400).HasSameValues(GammaRampBuilder.Build(3400)));
        Assert.False(GammaRampBuilder.Build(3400).HasSameValues(GammaRampBuilder.Build(3500)));
    }

    [Fact]
    public void EncodedGainsAreWeakerThanLinearOnes()
    {
        // Encoded values are what the LUT holds, and the display raises them to
        // roughly 2.2. Applying the linear gain directly to them -- the common
        // shortcut -- would over-tint by about that factor.
        (double _, double _, double linearBlue) = PlanckianLocus.LinearGainsFor(3000);
        (double _, double _, double encodedBlue) = PlanckianLocus.EncodedGainsFor(3000);

        Assert.True(linearBlue < 1d);
        Assert.True(encodedBlue > linearBlue, "the encoded gain must sit above the linear one");
        Assert.Equal(Math.Pow(linearBlue, 1d / 2.2d), encodedBlue, precision: 10);
    }
}
