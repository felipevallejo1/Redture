namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// Names applications known to write the display's colour lookup table.
/// </summary>
/// <remarks>
/// This is only ever used to make a message actionable — "f.lux is running" is
/// far more useful than "something is changing your colours". The detection of
/// the conflict itself is empirical, by reading the LUT back, so a tool nobody
/// listed here is still caught; it simply gets a generic name.
/// </remarks>
public interface IColorConflictDetector
{
    /// <summary>Known colour-management applications currently running.</summary>
    IReadOnlyList<string> FindRunningColorApplications();
}
