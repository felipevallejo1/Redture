using Redture.Core.Scheduling;
using Xunit;

namespace Redture.Core.Tests.Scheduling;

/// <summary>
/// Checks the sunrise equation against properties of the solar system rather
/// than against a table of almanac values.
/// </summary>
/// <remarks>
/// Astronomical constants copied from a reference and then asserted back are
/// circular. Facts like "the day is twelve hours long at the equator on an
/// equinox" or "the sun never sets inside the Arctic circle in June" hold
/// regardless of implementation, so an error in the algorithm cannot satisfy
/// them by accident.
/// </remarks>
public sealed class SolarCalculatorTests
{
    private static readonly GeoCoordinates Equator = new(0d, 0d);
    private static readonly GeoCoordinates BuenosAires = new(-34.6037, -58.3816);
    private static readonly GeoCoordinates London = new(51.5072, -0.1276);
    private static readonly GeoCoordinates AboveTheArcticCircle = new(78.2232, 15.6469);

    [Theory]
    [InlineData(0d, 0d)]            // Greenwich: solar noon at 12:00 UTC.
    [InlineData(-34.6037, -58.3816)] // Buenos Aires: nearly four hours later.
    [InlineData(51.5072, -0.1276)]   // London.
    [InlineData(35.6762, 139.6503)]  // Tokyo: nine hours earlier.
    [InlineData(-33.8688, 151.2093)] // Sydney.
    public void SolarNoonLandsWhereTheLongitudeSaysItShould(double latitude, double longitude)
    {
        // The test that was missing, and whose absence let a sign error ship.
        //
        // Every other test here checks the *length* of the day or the ordering
        // of its events, and a longitude applied with the wrong sign changes
        // neither -- it slides the whole day sideways by twice the correction
        // and leaves it exactly as long as it should be. Only an absolute time
        // catches that.
        SolarTimes times = Require(new DateOnly(2026, 7, 27), new GeoCoordinates(latitude, longitude));

        double expectedUtcHour = 12d - (longitude / 15d);
        double actualUtcHour = times.SolarNoon.UtcDateTime.TimeOfDay.TotalHours;

        double difference = Math.Abs(WrapToHalfDay(actualUtcHour - expectedUtcHour));

        // The equation of time -- the Earth's orbit being elliptical and its
        // axis tilted -- moves apparent noon by at most about a quarter of an
        // hour either way across the year.
        Assert.True(
            difference < 0.35,
            $"solar noon at longitude {longitude} was {actualUtcHour:0.00} h UTC, expected about {expectedUtcHour:0.00} h");
    }

    private static double WrapToHalfDay(double hours)
    {
        while (hours > 12d)
        {
            hours -= 24d;
        }

        while (hours < -12d)
        {
            hours += 24d;
        }

        return hours;
    }

    [Fact]
    public void SunriseAndSunsetAreSymmetricAboutSolarNoon()
    {
        SolarTimes times = Require(new DateOnly(2026, 4, 12), BuenosAires);

        TimeSpan morning = times.SolarNoon - times.Sunrise;
        TimeSpan afternoon = times.Sunset - times.SolarNoon;

        Assert.True(
            Math.Abs((morning - afternoon).TotalSeconds) < 2d,
            $"morning {morning} and afternoon {afternoon} should mirror each other");
    }

    [Theory]
    [InlineData(2026, 3, 20)]
    [InlineData(2026, 9, 22)]
    public void TheEquatorGetsTwelveHoursOfDaylightAtAnEquinox(int year, int month, int day)
    {
        SolarTimes times = Require(new DateOnly(year, month, day), Equator);

        // Not exactly twelve: refraction and the radius of the solar disc buy a
        // few extra minutes, which is precisely what the -0.833 degree
        // correction accounts for.
        Assert.InRange(times.DayLength.TotalHours, 12.0, 12.2);
    }

    [Fact]
    public void TheHemispheresHaveOppositeSeasons()
    {
        DateOnly juneSolstice = new(2026, 6, 21);

        SolarTimes northern = Require(juneSolstice, London);
        SolarTimes southern = Require(juneSolstice, BuenosAires);

        Assert.True(
            northern.DayLength > TimeSpan.FromHours(16),
            $"London should have a long June day, got {northern.DayLength}");

        Assert.True(
            southern.DayLength < TimeSpan.FromHours(10),
            $"Buenos Aires should have a short June day, got {southern.DayLength}");
    }

    [Fact]
    public void DayLengthMatchesRealityForAKnownPlaceAndDate()
    {
        // Buenos Aires at the June solstice: its shortest day, a little under
        // ten hours. A sanity check against the world, not against the code.
        SolarTimes times = Require(new DateOnly(2026, 6, 21), BuenosAires);

        Assert.InRange(times.DayLength.TotalHours, 9.5, 10.1);
    }

    [Fact]
    public void DaysAreLongerInSummerThanInWinter()
    {
        SolarTimes summer = Require(new DateOnly(2026, 6, 21), London);
        SolarTimes winter = Require(new DateOnly(2026, 12, 21), London);

        Assert.True(
            summer.DayLength > winter.DayLength + TimeSpan.FromHours(6),
            $"summer {summer.DayLength} should dwarf winter {winter.DayLength}");
    }

    [Fact]
    public void ThePolarSummerHasNoSunset()
    {
        // Above the Arctic circle in June the sun simply does not set. Returning
        // null rather than a fabricated time is what lets the scheduler fall
        // back instead of scheduling a transition that never comes.
        Assert.Null(SolarCalculator.Calculate(new DateOnly(2026, 6, 21), AboveTheArcticCircle));
    }

    [Fact]
    public void ThePolarWinterHasNoSunrise()
    {
        Assert.Null(SolarCalculator.Calculate(new DateOnly(2026, 12, 21), AboveTheArcticCircle));
    }

    [Fact]
    public void SunriseAlwaysPrecedesSunset()
    {
        for (int dayOfYear = 1; dayOfYear <= 365; dayOfYear += 7)
        {
            DateOnly date = new DateOnly(2026, 1, 1).AddDays(dayOfYear - 1);
            SolarTimes times = Require(date, BuenosAires);

            Assert.True(times.Sunrise < times.Sunset, $"sunrise after sunset on {date}");
            Assert.True(times.SolarNoon > times.Sunrise && times.SolarNoon < times.Sunset);
        }
    }

    [Theory]
    [InlineData(200d, 0d)]
    [InlineData(0d, 400d)]
    public void NonsensicalCoordinatesAreRejected(double latitude, double longitude)
    {
        Assert.Null(SolarCalculator.Calculate(new DateOnly(2026, 6, 21), new GeoCoordinates(latitude, longitude)));
    }

    private static SolarTimes Require(DateOnly date, GeoCoordinates location)
    {
        SolarTimes? times = SolarCalculator.Calculate(date, location);
        Assert.NotNull(times);
        return times.Value;
    }
}
