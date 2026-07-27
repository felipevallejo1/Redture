namespace Redture.Core.Scheduling;

/// <summary>
/// A point on the Earth, in degrees.
/// </summary>
/// <param name="Latitude">Degrees north of the equator; negative is south.</param>
/// <param name="Longitude">Degrees east of Greenwich; negative is west.</param>
/// <remarks>
/// Only ever used locally, to work out when the sun rises and sets. Redture
/// makes no network requests, so this never leaves the machine.
/// </remarks>
public readonly record struct GeoCoordinates(double Latitude, double Longitude)
{
    public bool IsValid =>
        Latitude is >= -90d and <= 90d &&
        Longitude is >= -180d and <= 180d;

    public override string ToString() => $"{Latitude:0.###}, {Longitude:0.###}";
}
