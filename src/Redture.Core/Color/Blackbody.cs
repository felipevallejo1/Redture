namespace Redture.Core.Color;

/// <summary>
/// Computes the colour of a black-body radiator by integrating Planck's law
/// against the CIE 1931 colour matching functions.
/// </summary>
/// <remarks>
/// <para>
/// This replaced a cubic-spline approximation of the Planckian locus. The
/// approximation was accurate and fast, but it is only <em>defined</em> from
/// 1667 K upwards, and that limit became the limit of how warm Redture could
/// go. Evaluating the physics directly has no such boundary: it is correct at
/// any temperature, which is what makes a deep red setting possible at all.
/// </para>
/// <para>
/// The colour matching functions use Wyman, Sloan and Shirley's multi-lobe
/// piecewise-Gaussian fit rather than a tabulated standard observer. It is
/// accurate to a small fraction of a just-noticeable difference here, and it
/// keeps the whole model to a page of arithmetic instead of several hundred
/// tabulated constants nobody can check by eye.
/// </para>
/// </remarks>
public static class Blackbody
{
    private const int MinWavelengthNm = 360;
    private const int MaxWavelengthNm = 830;
    private const int SampleCount = MaxWavelengthNm - MinWavelengthNm + 1;

    /// <summary>First radiation constant, W·m².</summary>
    private const double PlanckC1 = 3.741771852e-16;

    /// <summary>Second radiation constant, m·K.</summary>
    private const double PlanckC2 = 1.438776877e-2;

    /// <summary>
    /// Colour matching functions evaluated once per nanometre. The wavelengths
    /// never change, so the transcendental work is done at startup rather than
    /// on every step of a transition.
    /// </summary>
    private static readonly (double X, double Y, double Z)[] ColorMatchingFunctions = BuildColorMatchingTable();

    /// <summary>
    /// CIE 1931 chromaticity of a black body at <paramref name="kelvin"/>.
    /// </summary>
    public static (double X, double Y) Chromaticity(double kelvin)
    {
        (double bigX, double bigY, double bigZ) = Tristimulus(kelvin);

        double sum = bigX + bigY + bigZ;
        return sum <= 0d ? (0d, 0d) : (bigX / sum, bigY / sum);
    }

    /// <summary>
    /// Tristimulus values of a black body, in arbitrary but consistent units —
    /// only their ratios matter here.
    /// </summary>
    public static (double X, double Y, double Z) Tristimulus(double kelvin)
    {
        double x = 0d;
        double y = 0d;
        double z = 0d;

        for (int i = 0; i < SampleCount; i++)
        {
            double wavelengthNm = MinWavelengthNm + i;
            double radiance = SpectralRadiance(wavelengthNm, kelvin);

            (double cmfX, double cmfY, double cmfZ) = ColorMatchingFunctions[i];
            x += radiance * cmfX;
            y += radiance * cmfY;
            z += radiance * cmfZ;
        }

        return (x, y, z);
    }

    /// <summary>
    /// Planck's law: spectral radiant exitance of a black body, per wavelength.
    /// </summary>
    /// <remarks>
    /// Returned unnormalised. Absolute magnitude is irrelevant — it cancels when
    /// the tristimulus values are reduced to a chromaticity — and skipping the
    /// normalisation avoids the huge dynamic range the constants would
    /// otherwise produce at low temperatures.
    /// </remarks>
    private static double SpectralRadiance(double wavelengthNm, double kelvin)
    {
        double wavelengthM = wavelengthNm * 1e-9;
        double exponent = PlanckC2 / (wavelengthM * kelvin);

        // At low temperatures and short wavelengths this exponent grows large
        // enough to overflow. The physical answer there is "no light at all",
        // so returning zero is both safe and correct.
        if (exponent > 700d)
        {
            return 0d;
        }

        double denominator = Math.Exp(exponent) - 1d;
        if (denominator <= 0d)
        {
            return 0d;
        }

        return PlanckC1 / (Math.Pow(wavelengthM, 5) * denominator);
    }

    private static (double X, double Y, double Z)[] BuildColorMatchingTable()
    {
        (double X, double Y, double Z)[] table = new (double, double, double)[SampleCount];

        for (int i = 0; i < SampleCount; i++)
        {
            double wavelength = MinWavelengthNm + i;

            double x = (1.056 * Lobe(wavelength, 599.8, 37.9, 31.0))
                + (0.362 * Lobe(wavelength, 442.0, 16.0, 26.7))
                - (0.065 * Lobe(wavelength, 501.1, 20.4, 26.2));

            double y = (0.821 * Lobe(wavelength, 568.8, 46.9, 40.5))
                + (0.286 * Lobe(wavelength, 530.9, 16.3, 31.1));

            double z = (1.217 * Lobe(wavelength, 437.0, 11.8, 36.0))
                + (0.681 * Lobe(wavelength, 459.0, 26.0, 13.8));

            table[i] = (x, y, z);
        }

        return table;
    }

    /// <summary>
    /// One piecewise-Gaussian lobe: a Gaussian whose width differs either side
    /// of its peak, which is what lets a handful of them fit the distinctly
    /// asymmetric shapes of the colour matching functions.
    /// </summary>
    private static double Lobe(double wavelength, double peak, double widthBelow, double widthAbove)
    {
        double width = wavelength < peak ? widthBelow : widthAbove;
        double t = (wavelength - peak) / width;
        return Math.Exp(-0.5 * t * t);
    }
}
