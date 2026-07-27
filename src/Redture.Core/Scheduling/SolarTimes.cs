namespace Redture.Core.Scheduling;

/// <summary>
/// When the sun crosses the horizon on a given day at a given place.
/// </summary>
/// <param name="Sunrise">Moment the sun's upper limb reaches the horizon.</param>
/// <param name="Sunset">Moment it drops below it again.</param>
/// <param name="SolarNoon">Moment the sun is highest, midway between the two.</param>
public readonly record struct SolarTimes(
    DateTimeOffset Sunrise,
    DateTimeOffset Sunset,
    DateTimeOffset SolarNoon)
{
    /// <summary>How long the sun is above the horizon.</summary>
    public TimeSpan DayLength => Sunset - Sunrise;
}
