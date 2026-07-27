namespace Redture.Core.Scheduling;

/// <summary>
/// Works out sunrise and sunset for a date and place, with no network access.
/// </summary>
/// <remarks>
/// <para>
/// Implements the standard sunrise equation: mean solar time, the sun's mean
/// anomaly and equation of centre, then the hour angle at which the sun's upper
/// limb meets the horizon.
/// </para>
/// <para>
/// Accurate to about a minute for most of the inhabited world, which is far
/// more precision than a screen tint needs — the transition around it lasts an
/// hour by default.
/// </para>
/// </remarks>
public static class SolarCalculator
{
    /// <summary>Julian date of the J2000.0 epoch.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>Julian date of the Unix epoch.</summary>
    private const double UnixEpochJulianDate = 2440587.5;

    /// <summary>Obliquity of the ecliptic, degrees.</summary>
    private const double EarthObliquity = 23.4397;

    /// <summary>
    /// Solar elevation counted as sunrise or sunset, degrees.
    /// </summary>
    /// <remarks>
    /// Negative because the sun is already below the geometric horizon at that
    /// moment: the figure accounts for atmospheric refraction bending its light
    /// over the horizon, plus the radius of the solar disc, since sunrise is
    /// when the <em>upper limb</em> appears rather than the centre.
    /// </remarks>
    private const double SunriseElevation = -0.833;

    /// <summary>
    /// Sunrise and sunset for <paramref name="date"/>, or null where the sun
    /// does not cross the horizon at all — polar day and polar night, which are
    /// ordinary conditions above the Arctic and Antarctic circles rather than
    /// error cases.
    /// </summary>
    public static SolarTimes? Calculate(DateOnly date, GeoCoordinates location)
    {
        if (!location.IsValid)
        {
            return null;
        }

        double julianDay = Math.Ceiling(ToJulianDate(date) - J2000 + 0.0008);

        // Mean solar time at this longitude, in days since J2000.
        //
        // Subtracted, not added. Longitude here is east-positive, and a place
        // to the east sees noon *earlier* in UTC: at 58 degrees east the sun is
        // overhead at 08:07 UTC, not 15:53. Getting this backwards displaces
        // the whole day by twice the correction while leaving its length
        // untouched, which is a remarkably quiet way to be wrong.
        double meanSolarTime = julianDay - (location.Longitude / 360d);

        double meanAnomaly = Normalise(357.5291 + (0.98560028 * meanSolarTime));

        // Equation of centre: the correction from a circular orbit to the
        // Earth's actual elliptical one.
        double equationOfCentre =
            (1.9148 * Sin(meanAnomaly))
            + (0.0200 * Sin(2 * meanAnomaly))
            + (0.0003 * Sin(3 * meanAnomaly));

        // 102.9372 is the argument of perihelion; 180 flips from the Earth's
        // position as seen from the sun to the sun's as seen from Earth.
        double eclipticLongitude = Normalise(meanAnomaly + equationOfCentre + 180d + 102.9372);

        double solarNoonJulian = J2000
            + meanSolarTime
            + (0.0053 * Sin(meanAnomaly))
            - (0.0069 * Sin(2 * eclipticLongitude));

        double declination = Math.Asin(Sin(eclipticLongitude) * Sin(EarthObliquity));

        double latitudeRadians = ToRadians(location.Latitude);
        double hourAngleCosine =
            (Sin(SunriseElevation) - (Math.Sin(latitudeRadians) * Math.Sin(declination)))
            / (Math.Cos(latitudeRadians) * Math.Cos(declination));

        // Outside [-1, 1] the horizon is never crossed: the sun stays up all
        // day or never comes up at all.
        if (hourAngleCosine is > 1d or < -1d)
        {
            return null;
        }

        double hourAngleDegrees = ToDegrees(Math.Acos(hourAngleCosine));
        double fractionOfDay = hourAngleDegrees / 360d;

        return new SolarTimes(
            Sunrise: FromJulianDate(solarNoonJulian - fractionOfDay),
            Sunset: FromJulianDate(solarNoonJulian + fractionOfDay),
            SolarNoon: FromJulianDate(solarNoonJulian));
    }

    private static double ToJulianDate(DateOnly date)
    {
        DateTimeOffset midnightUtc = new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return (midnightUtc.ToUnixTimeMilliseconds() / 86_400_000d) + UnixEpochJulianDate;
    }

    private static DateTimeOffset FromJulianDate(double julianDate)
    {
        double unixMilliseconds = (julianDate - UnixEpochJulianDate) * 86_400_000d;
        return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(unixMilliseconds));
    }

    private static double Sin(double degrees) => Math.Sin(ToRadians(degrees));

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double ToDegrees(double radians) => radians * 180d / Math.PI;

    /// <summary>Wraps an angle into [0, 360).</summary>
    private static double Normalise(double degrees)
    {
        double wrapped = degrees % 360d;
        return wrapped < 0d ? wrapped + 360d : wrapped;
    }
}
