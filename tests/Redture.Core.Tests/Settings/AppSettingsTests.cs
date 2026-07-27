using Redture.Core.Settings;
using Xunit;

namespace Redture.Core.Tests.Settings;

public sealed class AppSettingsTests
{
    [Theory]
    [InlineData(-50, AppSettings.MinBrightness)]
    [InlineData(0, AppSettings.MinBrightness)]
    [InlineData(55, 55)]
    [InlineData(100, AppSettings.MaxBrightness)]
    [InlineData(1000, AppSettings.MaxBrightness)]
    public void Normalize_ClampsBrightness(double input, double expected)
    {
        AppSettings settings = new() { Brightness = input };

        settings.Normalize();

        Assert.Equal(expected, settings.Brightness);
    }

    [Theory]
    [InlineData(0, AppSettings.MinTemperatureKelvin)]
    [InlineData(3400, 3400)]
    [InlineData(99999, AppSettings.MaxTemperatureKelvin)]
    public void Normalize_ClampsTemperature(int input, int expected)
    {
        AppSettings settings = new() { TemperatureKelvin = input };

        settings.Normalize();

        Assert.Equal(expected, settings.TemperatureKelvin);
    }

    [Fact]
    public void Normalize_NeverAllowsAFullyOpaqueOverlay()
    {
        AppSettings settings = new() { MaxOverlayOpacity = 1.0 };

        settings.Normalize();

        Assert.True(settings.MaxOverlayOpacity < 1.0);
        Assert.Equal(AppSettings.AbsoluteMaxOverlayOpacity, settings.MaxOverlayOpacity);
    }

    [Fact]
    public void Normalize_RepairsAnUnknownSchemaVersion()
    {
        AppSettings settings = new() { SchemaVersion = 999 };

        settings.Normalize();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Defaults_AreNeutral()
    {
        AppSettings settings = new();

        // A fresh install must not change how the screen looks until the user
        // asks for it.
        Assert.Equal(AppSettings.MaxBrightness, settings.Brightness);
        Assert.Equal(AppSettings.NeutralTemperatureKelvin, settings.TemperatureKelvin);
        Assert.False(settings.AutomationEnabled);
        Assert.False(settings.StartWithSystem);
        Assert.False(settings.ExtendedGammaRangeOptIn);
    }

    [Fact]
    public void Clone_ProducesAnIndependentCopy()
    {
        AppSettings original = new() { Brightness = 30 };

        AppSettings copy = original.Clone();
        copy.Brightness = 90;

        Assert.Equal(30, original.Brightness);
    }
}
