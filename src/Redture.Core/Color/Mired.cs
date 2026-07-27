namespace Redture.Core.Color;

/// <summary>
/// Colour temperature expressed in mired — reciprocal megakelvin.
/// </summary>
/// <remarks>
/// <para>
/// Every transition Redture performs is interpolated here rather than in
/// kelvin, because equal steps in mired look like equal steps to the eye and
/// equal steps in kelvin do not. The difference between 2000 K and 2500 K is
/// obvious; the difference between 6000 K and 6500 K is barely visible, yet
/// both are 500 K.
/// </para>
/// <para>
/// Interpolating a sunset linearly in kelvin therefore lurches at the warm end
/// and crawls at the cool one. In mired the same transition is perceptually
/// even from beginning to end.
/// </para>
/// </remarks>
public static class Mired
{
    private const double MiredScale = 1_000_000d;

    public static double FromKelvin(double kelvin) => MiredScale / kelvin;

    public static double ToKelvin(double mired) => MiredScale / mired;

    /// <summary>
    /// Interpolates between two colour temperatures, evenly in perception.
    /// </summary>
    /// <param name="progress">0 gives <paramref name="fromKelvin"/>, 1 gives
    /// <paramref name="toKelvin"/>; values outside are clamped.</param>
    public static int Interpolate(int fromKelvin, int toKelvin, double progress)
    {
        progress = Math.Clamp(progress, 0d, 1d);

        double from = FromKelvin(fromKelvin);
        double to = FromKelvin(toKelvin);

        return (int)Math.Round(ToKelvin(from + ((to - from) * progress)));
    }
}
