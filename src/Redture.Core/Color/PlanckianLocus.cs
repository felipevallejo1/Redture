namespace Redture.Core.Color;

/// <summary>
/// Converts a colour temperature in kelvin into per-channel RGB gains.
/// </summary>
/// <remarks>
/// <para>
/// The chain is: temperature → CIE 1931 chromaticity of a black body → XYZ →
/// linear sRGB → gains relative to the neutral white point.
/// </para>
/// <para>
/// The chromaticity comes from <see cref="Blackbody"/>, which integrates
/// Planck's law directly. An earlier version used a published cubic-spline
/// approximation instead; it was accurate and cheaper, but only defined from
/// 1667 K upwards, and that limit became a limit on how warm the application
/// could go.
/// </para>
/// </remarks>
public static class PlanckianLocus
{
    /// <summary>
    /// Lowest temperature evaluated, and the point at which the model
    /// saturates.
    /// </summary>
    /// <remarks>
    /// Not a limit of the physics, which is valid at any temperature, but the
    /// temperature below which nothing further happens. Measured rather than
    /// assumed: green reaches exactly zero here, so the display is emitting
    /// red and nothing else, and every lower temperature produces an identical
    /// ramp. For reference, green is still at 0.030 at 1000 K and 0.205 at
    /// 1700 K, so the last two hundred kelvin are where an amber screen finally
    /// becomes a red one.
    /// </remarks>
    public const double MinimumKelvin = 800d;

    /// <summary>Highest temperature offered.</summary>
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

    /// <summary>CIE 1931 chromaticity of a black body, clamped to the offered range.</summary>
    private static (double X, double Y) Chromaticity(double kelvin) =>
        Blackbody.Chromaticity(Math.Clamp(kelvin, MinimumKelvin, MaximumKelvin));
}
