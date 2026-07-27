using Redture.Core.Color;
using Redture.Core.Settings;
using Xunit;

namespace Redture.Core.Tests.Color;

/// <summary>
/// Validates the spectral black-body model against an independent reference.
/// </summary>
/// <remarks>
/// The production code integrates Planck's law against analytic fits of the CIE
/// colour matching functions. This file carries a completely different
/// derivation — Kim, Moon, Hwang and Kim's published cubic-spline approximation
/// of the Planckian locus — and checks the two agree.
/// <para>
/// That is the point of the exercise. Two implementations sharing no code, no
/// constants and no method, landing on the same chromaticity, is real evidence
/// that both are right. A test that asserted the spectral model returns what
/// the spectral model returns would be evidence of nothing.
/// </para>
/// </remarks>
public sealed class BlackbodyTests
{
    /// <summary>
    /// Largest chromaticity difference tolerated between the two derivations.
    /// A just-noticeable difference in CIE 1931 xy is roughly 0.004 in the
    /// region of interest, so agreeing this closely means no one could see the
    /// difference between them.
    /// </summary>
    private const double Tolerance = 0.006;

    [Theory]
    [InlineData(1700)]
    [InlineData(2000)]
    [InlineData(2400)]
    [InlineData(2700)]
    [InlineData(3000)]
    [InlineData(3400)]
    [InlineData(4000)]
    [InlineData(5000)]
    [InlineData(6500)]
    [InlineData(9000)]
    [InlineData(15000)]
    [InlineData(25000)]
    public void SpectralModelAgreesWithThePublishedApproximation(int kelvin)
    {
        (double actualX, double actualY) = Blackbody.Chromaticity(kelvin);
        (double expectedX, double expectedY) = ReferenceChromaticity(kelvin);

        Assert.True(
            Math.Abs(actualX - expectedX) < Tolerance,
            $"x at {kelvin} K: spectral {actualX:0.0000} vs reference {expectedX:0.0000}");

        Assert.True(
            Math.Abs(actualY - expectedY) < Tolerance,
            $"y at {kelvin} K: spectral {actualY:0.0000} vs reference {expectedY:0.0000}");
    }

    [Fact]
    public void TheModelStillWorksBelowTheApproximationsDomain()
    {
        // The whole reason for replacing the approximation: it is undefined
        // below 1667 K, and that was the limit on how warm Redture could go.
        (double x, double y) = Blackbody.Chromaticity(1000);

        Assert.InRange(x, 0.6, 0.75);   // Deep in the red corner of the diagram.
        Assert.InRange(y, 0.28, 0.4);
    }

    [Fact]
    public void ChromaticityMovesTowardsRedAsTemperatureFalls()
    {
        double previousX = double.MinValue;

        for (int kelvin = 25000; kelvin >= 1000; kelvin -= 100)
        {
            (double x, double _) = Blackbody.Chromaticity(kelvin);
            Assert.True(x > previousX, $"x should rise as temperature falls, but fell at {kelvin} K");
            previousX = x;
        }
    }

    [Fact]
    public void GreenKeepsFallingAllTheWayDown()
    {
        // Blue is fully extinguished by roughly 2000 K, so below that the only
        // thing that can still make the screen redder is losing green. If this
        // flattened out, the extra range would be decorative.
        (double _, double greenAt2000, double _) = PlanckianLocus.LinearGainsFor(2000);
        (double _, double greenAt1500, double _) = PlanckianLocus.LinearGainsFor(1500);
        (double _, double greenAt1000, double _) = PlanckianLocus.LinearGainsFor(1000);

        Assert.True(greenAt1500 < greenAt2000, $"green did not fall from 2000 K ({greenAt2000:0.000}) to 1500 K ({greenAt1500:0.000})");
        Assert.True(greenAt1000 < greenAt1500, $"green did not fall from 1500 K ({greenAt1500:0.000}) to 1000 K ({greenAt1000:0.000})");
    }

    [Fact]
    public void TheWarmestSettingEmitsRedAndNothingElse()
    {
        // What the bottom of the slider is for: not a warm white, an actually
        // red screen.
        (double red, double green, double blue) = PlanckianLocus.LinearGainsFor(AppSettings.MinTemperatureKelvin);

        Assert.Equal(1d, red, precision: 10);
        Assert.Equal(0d, green, precision: 10);
        Assert.Equal(0d, blue, precision: 10);
    }

    [Fact]
    public void TheFloorIsWhereTheModelStopsChanging()
    {
        // Justifies where the slider ends. If lower temperatures still moved
        // the result, the range would be cut short for no reason; if higher
        // ones had already saturated, the last stretch would be dead travel.
        GammaRamp atFloor = GammaRampBuilder.Build(AppSettings.MinTemperatureKelvin);
        GammaRamp wellBelowFloor = GammaRampBuilder.Build(AppSettings.MinTemperatureKelvin - 400);
        GammaRamp justAboveFloor = GammaRampBuilder.Build(AppSettings.MinTemperatureKelvin + 200);

        Assert.True(atFloor.HasSameValues(wellBelowFloor), "temperatures below the floor should be indistinguishable");
        Assert.False(atFloor.HasSameValues(justAboveFloor), "the floor should not already have saturated");
    }

    /// <summary>
    /// Kim et al.'s cubic-spline approximation of the Planckian locus, valid
    /// from 1667 K to 25000 K. Kept here, in the tests, rather than in the
    /// production code: its only job is to be a second opinion.
    /// </summary>
    private static (double X, double Y) ReferenceChromaticity(double kelvin)
    {
        double t = kelvin;
        double t2 = t * t;
        double t3 = t2 * t;

        double x = t <= 4000d
            ? (-0.2661239e9 / t3) - (0.2343589e6 / t2) + (0.8776956e3 / t) + 0.179910
            : (-3.0258469e9 / t3) + (2.1070379e6 / t2) + (0.2226347e3 / t) + 0.240390;

        double x2 = x * x;
        double x3 = x2 * x;

        double y = t switch
        {
            <= 2222d => (-1.1063814 * x3) - (1.34811020 * x2) + (2.18555832 * x) - 0.20219683,
            <= 4000d => (-0.9549476 * x3) - (1.37418593 * x2) + (2.09137015 * x) - 0.16748867,
            _ => (3.0817580 * x3) - (5.87338670 * x2) + (3.75112997 * x) - 0.37001483,
        };

        return (x, y);
    }
}
