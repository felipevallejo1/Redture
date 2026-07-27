using Redture.Core.Settings;

namespace Redture.Core.Color;

/// <summary>
/// Turns a colour temperature into the lookup table a display needs.
/// </summary>
public static class GammaRampBuilder
{
    /// <summary>
    /// Builds the ramp for a temperature in kelvin. At
    /// <see cref="AppSettings.NeutralTemperatureKelvin"/> the result is exactly
    /// <see cref="GammaRamp.Linear"/>, so "neutral" really means untouched
    /// rather than very slightly tinted.
    /// </summary>
    public static GammaRamp Build(int kelvin)
    {
        int clamped = Math.Clamp(
            kelvin,
            AppSettings.MinTemperatureKelvin,
            AppSettings.MaxTemperatureKelvin);

        (double r, double g, double b) = PlanckianLocus.EncodedGainsFor(clamped);
        return GammaRamp.Create(r, g, b);
    }
}
