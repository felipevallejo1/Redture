using Redture.Core.Settings;

namespace Redture.Core.Scheduling;

/// <summary>
/// How the time-of-day automation should behave.
/// </summary>
public sealed class ScheduleSettings
{
    /// <summary>Shortest transition offered, minutes.</summary>
    public const int MinTransitionMinutes = 1;

    /// <summary>Longest transition offered, minutes.</summary>
    public const int MaxTransitionMinutes = 240;

    /// <summary>
    /// Whether the anchors come from the sun's actual position rather than
    /// fixed clock times. Falls back to the manual times when no location has
    /// been set, or on days when the sun does not cross the horizon at all.
    /// </summary>
    public bool UseSolarTimes { get; set; } = true;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Anchor used when solar times are off or unavailable.</summary>
    public TimeOnly ManualSunrise { get; set; } = new(7, 0);

    /// <summary>Anchor used when solar times are off or unavailable.</summary>
    public TimeOnly ManualSunset { get; set; } = new(20, 0);

    /// <summary>Colour temperature held during the day.</summary>
    public int DayTemperatureKelvin { get; set; } = AppSettings.NeutralTemperatureKelvin;

    /// <summary>Colour temperature held overnight.</summary>
    public int NightTemperatureKelvin { get; set; } = 3000;

    /// <summary>
    /// How long the change takes, starting at each anchor. An hour by default:
    /// slow enough that no single moment of it is noticeable, which is the
    /// whole trick to a tint nobody thinks about.
    /// </summary>
    public int TransitionMinutes { get; set; } = 60;

    /// <summary>The configured location, or null when it is unset or invalid.</summary>
    public GeoCoordinates? Location
    {
        get
        {
            if (Latitude is not { } latitude || Longitude is not { } longitude)
            {
                return null;
            }

            GeoCoordinates coordinates = new(latitude, longitude);
            return coordinates.IsValid ? coordinates : null;
        }
    }

    /// <summary>Whether solar anchors can actually be computed right now.</summary>
    public bool CanUseSolarTimes => UseSolarTimes && Location is not null;

    public void Normalize()
    {
        DayTemperatureKelvin = Math.Clamp(
            DayTemperatureKelvin,
            AppSettings.MinTemperatureKelvin,
            AppSettings.MaxTemperatureKelvin);

        NightTemperatureKelvin = Math.Clamp(
            NightTemperatureKelvin,
            AppSettings.MinTemperatureKelvin,
            AppSettings.MaxTemperatureKelvin);

        TransitionMinutes = Math.Clamp(TransitionMinutes, MinTransitionMinutes, MaxTransitionMinutes);

        if (Latitude is { } latitude && latitude is < -90d or > 90d)
        {
            Latitude = null;
        }

        if (Longitude is { } longitude && longitude is < -180d or > 180d)
        {
            Longitude = null;
        }
    }

    public ScheduleSettings Clone() => (ScheduleSettings)MemberwiseClone();
}
