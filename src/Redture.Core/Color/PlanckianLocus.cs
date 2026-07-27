namespace Redture.Core.Color;

/// <summary>
/// Converts a colour temperature in kelvin into per-channel RGB gains.
/// </summary>
/// <remarks>
/// <para>
/// The chain is: temperature → CIE 1931 chromaticity on the Planckian locus →
/// XYZ → linear sRGB → gains relative to the neutral white point.
/// </para>
/// <para>
/// The locus is evaluated with Kim et al.'s cubic-spline approximation rather
/// than by integrating Planck's law against the CIE colour-matching functions.
/// The approximation is accurate to well under a just-noticeable difference
/// across its domain and costs a handful of multiplications, which matters
/// because this runs on every step of a transition.
/// </para>
/// </remarks>
public static class PlanckianLocus
{
    /// <summary>
    /// Lowest temperature the approximation is defined for. Below this the
    /// published domain ends, and extrapolating a cubic outside the data it was
    /// fitted to produces chromaticities that leave the visible gamut entirely.
    /// </summary>
    public const double MinimumKelvin = 1667d;

    /// <summary>Highest temperature the approximation is defined for.</summary>
    public const double MaximumKelvin = 25000d;

    /// <summary>
    /// Reference white. Everything is expressed relative to this, which is what
    /// makes the neutral setting an exact identity rather than a very slight
    /// tint — the Planckian locus at 6500 K is close to, but not exactly, sRGB's
    /// D65 white point.
    /// </summary>
    public const double NeutralKelvin = 6500d;

    /// <summary>
    /// Display transfer exponent used to move between linear light and the
    /// encoded values a gamma ramp actually contains.
    /// </summary>
    /// <remarks>
    /// sRGB's true curve is piecewise, with a small linear segment near black;
    /// a pure 2.2 power differs from it by a fraction of a code value at the
    /// magnitudes involved here, and the LUT is only 8 bits in anyway.
    /// </remarks>
    private const double DisplayGamma = 2.2d;

    /// <summary>Linear-light white point at <see cref="NeutralKelvin"/>.</summary>
    private static readonly (double R, double G, double B) NeutralWhite = LinearWhitePoint(NeutralKelvin);

    /// <summary>
    /// Per-channel multipliers to apply to the values stored in a gamma ramp.
    /// </summary>
    /// <remarks>
    /// These are <em>encoded-domain</em> gains, not linear-light ones. To scale
    /// emitted light by a factor <c>g</c>, an encoded value must be scaled by
    /// <c>g^(1/γ)</c>, because the display raises whatever it receives to the
    /// power γ. Multiplying encoded values by the linear gain directly — which
    /// is the common shortcut — over-applies the tint by roughly a factor of γ
    /// in the shadows.
    /// </remarks>
    public static (double R, double G, double B) EncodedGainsFor(double kelvin)
    {
        (double r, double g, double b) = LinearGainsFor(kelvin);

        return (
            Math.Pow(r, 1d / DisplayGamma),
            Math.Pow(g, 1d / DisplayGamma),
            Math.Pow(b, 1d / DisplayGamma));
    }

    /// <summary>
    /// Linear-light gains, normalised so no channel is ever boosted above its
    /// original value.
    /// </summary>
    /// <remarks>
    /// Only ever attenuating matters: a gamma ramp cannot make a display emit
    /// more light than it already does, so a gain above 1 would simply clip the
    /// highlights and lose detail.
    /// </remarks>
    public static (double R, double G, double B) LinearGainsFor(double kelvin)
    {
        (double r, double g, double b) = LinearWhitePoint(kelvin);

        r /= NeutralWhite.R;
        g /= NeutralWhite.G;
        b /= NeutralWhite.B;

        double peak = Math.Max(r, Math.Max(g, b));
        if (peak > 0d)
        {
            r /= peak;
            g /= peak;
            b /= peak;
        }

        return (
            Math.Clamp(r, 0d, 1d),
            Math.Clamp(g, 0d, 1d),
            Math.Clamp(b, 0d, 1d));
    }

    /// <summary>
    /// The black-body radiator's colour at a given temperature, as linear sRGB
    /// primaries with luminance normalised to 1.
    /// </summary>
    private static (double R, double G, double B) LinearWhitePoint(double kelvin)
    {
        (double x, double y) = Chromaticity(kelvin);

        // xyY with Y = 1 back to tristimulus values.
        double bigY = 1d;
        double bigX = x / y;
        double bigZ = (1d - x - y) / y;

        // CIE XYZ to linear sRGB (D65).
        double r = (3.2404542 * bigX) - (1.5371385 * bigY) - (0.4985314 * bigZ);
        double g = (-0.9692660 * bigX) + (1.8760108 * bigY) + (0.0415560 * bigZ);
        double b = (0.0556434 * bigX) - (0.2040259 * bigY) + (1.0572252 * bigZ);

        // Temperatures near the ends of the locus fall outside the sRGB gamut,
        // which shows up as a negative primary. Clamping is the standard
        // resolution: the colour is simply not reproducible on this display.
        return (Math.Max(r, 0d), Math.Max(g, 0d), Math.Max(b, 0d));
    }

    /// <summary>
    /// CIE 1931 chromaticity of the Planckian locus, by the cubic-spline
    /// approximation of Kim, Moon, Hwang and Kim.
    /// </summary>
    private static (double X, double Y) Chromaticity(double kelvin)
    {
        double t = Math.Clamp(kelvin, MinimumKelvin, MaximumKelvin);
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
